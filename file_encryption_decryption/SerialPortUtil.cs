using System;
using System.Collections.Generic;
using System.Text;
using System.IO.Ports;
using System.Windows.Forms;
using System.Management;

namespace SA
{
    /// <summary>
    /// 串口开发辅助类
    /// </summary>
    public class SerialPortUtil
    {
        public static string datarecieve;       
        private StringBuilder stringBuilder = new StringBuilder();  //为了避免在接收处理函数中反复调用，依然声明为一个全局变量
        /// <summary>
        /// 接收事件是否有效 false表示有效
        /// </summary>
        public bool ReceiveEventFlag = false;
        /// <summary>
        /// 结束符比特
        /// </summary>
        public byte EndByte = 0x23;//string End = "#";

        /// <summary>
        /// 完整协议的记录处理事件
        /// </summary>
        //public event DataReceivedEventHandler DataReceived;
        public event SerialErrorReceivedEventHandler Error;
        /*新增12.5.18.24*/
        public bool isFind()
        {
            if (GetComNum() == -1)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        #region 变量属性
        //自动获取变量名
        public static string _nowportName =  "COM" + GetComNum();
        private string _portName = _nowportName;//串口号，默认读取CH340对应串口
        private SerialPortBaudRates _baudRate = SerialPortBaudRates.BaudRate_9600;//波特率
        private Parity _parity = Parity.None;//校验位
        private StopBits _stopBits = StopBits.One;//停止位
        private SerialPortDatabits _dataBits = SerialPortDatabits.EightBits;//数据位

        public static Action<string> ReceiveCmd;

        static public SerialPort comPort;

        /// <summary>
        /// 串口号
        /// </summary>
        public string PortName
        {
            get { return _portName; }
            set { _portName = value; }
        }

        /// <summary>
        /// 波特率
        /// </summary>
        public SerialPortBaudRates BaudRate
        {
            get { return _baudRate; }
            set { _baudRate = value; }
        }

        /// <summary>
        /// 奇偶校验位
        /// </summary>
        public Parity Parity
        {
            get { return _parity; }
            set { _parity = value; }
        }

        /// <summary>
        /// 数据位
        /// </summary>
        public SerialPortDatabits DataBits
        {
            get { return _dataBits; }
            set { _dataBits = value; }
        }

        /// <summary>
        /// 停止位
        /// </summary>
        public StopBits StopBits
        {
            get { return _stopBits; }
            set { _stopBits = value; }
        }

        #endregion

        #region 构造函数

        /// <summary>
        /// 参数构造函数（使用枚举参数构造）
        /// </summary>
        /// <param name="baud">波特率</param>
        /// <param name="par">奇偶校验位</param>
        /// <param name="sBits">停止位</param>
        /// <param name="dBits">数据位</param>
        /// <param name="name">串口号</param>
        public SerialPortUtil(string name, SerialPortBaudRates baud, Parity par, SerialPortDatabits dBits, StopBits sBits)
        {
            _portName = name;
            _baudRate = baud;
            _parity = par;
            _dataBits = dBits;
            _stopBits = sBits;
            comPort = new SerialPort();
            comPort.DataReceived += new SerialDataReceivedEventHandler(comPort_DataReceived);
            comPort.ErrorReceived += new SerialErrorReceivedEventHandler(comPort_ErrorReceived);
        }

        /// <summary>
        /// 参数构造函数（使用字符串参数构造）
        /// </summary>
        /// <param name="baud">波特率</param>
        /// <param name="par">奇偶校验位</param>
        /// <param name="sBits">停止位</param>
        /// <param name="dBits">数据位</param>
        /// <param name="name">串口号</param>
        public SerialPortUtil(string name, string baud, string par, string dBits, string sBits)
        {
            _portName = name;
            _baudRate = (SerialPortBaudRates)Enum.Parse(typeof(SerialPortBaudRates), baud);
            _parity = (Parity)Enum.Parse(typeof(Parity), par);
            _dataBits = (SerialPortDatabits)Enum.Parse(typeof(SerialPortDatabits), dBits);
            _stopBits = (StopBits)Enum.Parse(typeof(StopBits), sBits);
            comPort = new SerialPort();
            comPort.DataReceived += new SerialDataReceivedEventHandler(comPort_DataReceived);
            comPort.ErrorReceived += new SerialErrorReceivedEventHandler(comPort_ErrorReceived);
        }

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public SerialPortUtil()
        {

            _portName = _nowportName;
            _baudRate = SerialPortBaudRates.BaudRate_9600;
            _parity = Parity.None;
            _dataBits = SerialPortDatabits.EightBits;
            _stopBits = StopBits.One;
            comPort = new SerialPort();
            comPort.DataReceived += new SerialDataReceivedEventHandler(comPort_DataReceived);
            comPort.ErrorReceived += new SerialErrorReceivedEventHandler(comPort_ErrorReceived);
        }

        #endregion

        /// <summary>
        /// 端口是否已经打开
        /// </summary>
        public bool IsOpen
        {
            get
            {
                return comPort.IsOpen;
            }
        }

        /// <summary>
        /// 打开端口
        /// </summary>
        /// <returns></returns>
        public  void OpenPort()
        {
            if (comPort.IsOpen)
            {
                comPort.Close();
            }
            
            comPort.PortName = _nowportName;
            comPort.BaudRate = (int)_baudRate;
            comPort.Parity = _parity;
            comPort.DataBits = (int)_dataBits;
            comPort.StopBits = _stopBits;
            try
            {
                comPort.Open();
            }
            catch (Exception)
            {
                Console.WriteLine("串口异常,串口未开启（未连接）");
            }

        }



        /// <summary>
        /// 关闭端口
        /// </summary>
        public void ClosePort()
        {
            if (comPort.IsOpen) comPort.Close();
        }

        /// <summary>
        /// 丢弃来自串行驱动程序的接收和发送缓冲区的数据
        /// </summary>
        public void DiscardBuffer()
        {
            comPort.DiscardInBuffer();
            comPort.DiscardOutBuffer();
        }

        /// <summary>
        /// 数据接收处理
        /// </summary>
        void comPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            int num = comPort.BytesToRead;      //获取接收缓冲区中的字节数
            byte[] received_buf = new byte[num];    //声明一个大小为num的字节数据用于存放读出的byte型数据
            comPort.Read(received_buf, 0, num);   //读取接收缓冲区中num个字节到byte数组中
            stringBuilder.Clear();     //防止出错,首先清空字符串构造器
                                       //选中ASCII模式显示
            stringBuilder.Append(Encoding.ASCII.GetString(received_buf));  //将整个数组解码为ASCII数组//因为要访问UI资源，所以需要使用invoke方式同步ui
            datarecieve = stringBuilder.ToString();
            ReceiveCmd?.Invoke(datarecieve);
        }

        /// <summary>
        /// 发送串口命令
        /// </summary>
        /// <param name="SendData">发送数据</param>
        /// <param name="ReceiveData">接收数据</param>
        /// <param name="Overtime">重复次数</param>
        /// <returns></returns>
        public void SendCommand(string SendData)
        {
            try
            {
                if (!(comPort.IsOpen)) comPort.Open();

                ReceiveEventFlag = true;        //关闭接收事件
                comPort.DiscardInBuffer();      //清空接收缓冲区   
                try
                {
                    comPort.Write(SendData);
                }
                catch (Exception)
                {

                    throw;
                }
                ReceiveEventFlag = false;       //打开事件
            }
            catch (Exception)
            {
                MessageBox.Show("端口不存在！");
            }


        }
        /*  /// <summary>
          /// 获取接收到的数据
          /// </summary>
          /// <returns></returns>
          public string GetReceivedData()
          {
              return datarecieve;
          }*/

        /// <summary>
        /// 错误处理函数
        /// </summary>
        void comPort_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            if (Error != null)
            {
                Error(sender, e);
            }
        }

        #region 数据写入操作

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="msg"></param>
        public void WriteData(string msg)
        {
            if (!(comPort.IsOpen)) comPort.Open();

            comPort.Write(msg);
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="msg">写入端口的字节数组</param>
        public void WriteData(byte[] msg)
        {
          
            if (!(comPort.IsOpen)) comPort.Open();

            comPort.Write(msg, 0, msg.Length);
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="msg">包含要写入端口的字节数组</param>
        /// <param name="offset">参数从0字节开始的字节偏移量</param>
        /// <param name="count">要写入的字节数</param>
        public void WriteData(byte[] msg, int offset, int count)
        {
            if (!(comPort.IsOpen)) comPort.Open();

            comPort.Write(msg, offset, count);
        }

        

        #endregion

        #region 常用的列表数据获取和绑定操作

        /// <summary>
        /// 封装获取串口号列表
        /// </summary>
        /// <returns></returns>
        public static string[] GetPortNames()
        {
            return SerialPort.GetPortNames();
        }

        /// <summary>
        /// 设置串口号
        /// </summary>
        /// <param name="obj"></param>
        public static void SetPortNameValues(ComboBox obj)
        {
            obj.Items.Clear();
            foreach (string str in SerialPort.GetPortNames())
            {
                obj.Items.Add(str);
            }
        }

        /// <summary>
        /// 设置波特率
        /// </summary>
        public static void SetBauRateValues(ComboBox obj)
        {
            obj.Items.Clear();
            foreach (SerialPortBaudRates rate in Enum.GetValues(typeof(SerialPortBaudRates)))
            {
                obj.Items.Add(((int)rate).ToString());
            }
        }

        /// <summary>
        /// 设置数据位
        /// </summary>
        public static void SetDataBitsValues(ComboBox obj)
        {
            obj.Items.Clear();
            foreach (SerialPortDatabits databit in Enum.GetValues(typeof(SerialPortDatabits)))
            {
                obj.Items.Add(((int)databit).ToString());
            }
        }

        /// <summary>
        /// 设置校验位列表
        /// </summary>
        public static void SetParityValues(ComboBox obj)
        {
            obj.Items.Clear();
            foreach (string str in Enum.GetNames(typeof(Parity)))
            {
                obj.Items.Add(str);
            }
        }

        /// <summary>
        /// 设置停止位
        /// </summary>
        public static void SetStopBitValues(ComboBox obj)
        {
            obj.Items.Clear();
            foreach (string str in Enum.GetNames(typeof(StopBits)))
            {
                obj.Items.Add(str);
            }
        }

        #endregion

        #region 格式转换
        /// <summary>
        /// 转换十六进制字符串到字节数组
        /// </summary>
        /// <param name="msg">待转换字符串</param>
        /// <returns>字节数组</returns>
        public static byte[] HexToByte(string msg)
        {
            msg = msg.Replace(" ", "");//移除空格

            //create a byte array the length of the
            //divided by 2 (Hex is 2 characters in length)
            byte[] comBuffer = new byte[msg.Length / 2];
            for (int i = 0; i < msg.Length; i += 2)
            {
                //convert each set of 2 characters to a byte and add to the array
                comBuffer[i / 2] = (byte)Convert.ToByte(msg.Substring(i, 2), 16);
            }

            return comBuffer;
        }

        /// <summary>
        /// 转换字节数组到十六进制字符串
        /// </summary>
        /// <param name="comByte">待转换字节数组</param>
        /// <returns>十六进制字符串</returns>
        public static string ByteToHex(byte[] comByte)
        {
            StringBuilder builder = new StringBuilder(comByte.Length * 3);
            foreach (byte data in comByte)
            {
                builder.Append(Convert.ToString(data, 16).PadLeft(2, '0').PadRight(3, ' '));
            }

            return builder.ToString().ToUpper();
        }
        #endregion

        /// <summary>
        /// 检查端口名称是否存在
        /// </summary>
        /// <param name="port_name"></param>
        /// <returns></returns>
        public static bool Exists(string port_name)
        {
            foreach (string port in SerialPort.GetPortNames()) if (port == port_name) return true;
            return false;
        }

        /// <summary>
        /// 格式化端口相关属性
        /// </summary>
        /// <param name="port"></param>
        /// <returns></returns>
        public static string Format(SerialPort port)
        {
            return String.Format("{0} ({1},{2},{3},{4},{5})",
                port.PortName, port.BaudRate, port.DataBits, port.StopBits, port.Parity, port.Handshake);
        }

        #region 获取串口编号

        /// <summary>
        /// 获取目标com编号
        /// </summary>
        /// <returns></returns>
        public static int GetComNum()
        {
            int comNum = -1;
            string[] strArr = GetHarewareInfo(HardwareEnum.Win32_PnPEntity, "Name");
            foreach (string s in strArr)
            {
                if (s.Length >= 23 && s.Contains("CH340"))
                {
                    int start = s.IndexOf("(") + 3;
                    int end = s.IndexOf(")");
                    comNum = Convert.ToInt32(s.Substring(start + 1, end - start - 1));
                }
            }
            return  comNum;
        }



        ///<summary>
        ///获取所有com编号
        /// </summary>
        ///<returns></returns>
        public static string[] GetAllCom()
        {
            int i=0;
            string[] AllCom=new string[30];
            string Com = "";
            string[] strArr1 = GetHarewareInfo(HardwareEnum.Win32_PnPEntity, "Name");
            foreach (string s1 in strArr1)
            {
                if(s1.Contains("(COM"))
                {
                    int start = s1.IndexOf("(");
                    int end = s1.IndexOf(")");
                    Com = Convert.ToString(s1.Substring(start+1,end-start-1));
                    AllCom[i] = Com;
                    i++;
                }
            }
            return AllCom ;
        }











        /// <summary>
        /// 使用windowsapi获取系统设备信息。
        /// </summary>
        /// <param name="hardType">Device type.</param>
        /// <param name="propKey">the property of the device.</param>
        /// <returns></returns>
        private static string[] GetHarewareInfo(HardwareEnum hardType, string propKey)
        {
            List<string> strs = new List<string>();
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("select * from " + hardType))
                {
                    var hardInfos = searcher.Get();
                    foreach (var hardInfo in hardInfos)
                    {
                        if (hardInfo.Properties[propKey].Value != null)
                        {
                            String str = hardInfo.Properties[propKey].Value.ToString();
                            strs.Add(str);
                        }
                    }
                }
                return strs.ToArray();
            }
            catch
            {
                return null;
            }
            finally
            {
                strs = null;
            }
        }

        /// <summary>
        /// 枚举win32 api
        /// </summary>
        public enum HardwareEnum
        {
            //Win32_ParallelPort, // 并口
            Win32_SerialPort, // 串口
            //Win32_SerialPortConfiguration, // 串口配置
            //Win32_SoundDevice, // 多媒体设置，一般指声卡。
            //Win32_SystemSlot, // 主板插槽 (ISA & PCI & AGP)
            //Win32_USBController, // USB 控制器
            //Win32_NetworkAdapter, // 网络适配器
            //Win32_NetworkAdapterConfiguration, // 网络适配器设置
            //Win32_Printer, // 打印机
            //Win32_PrinterConfiguration, // 打印机设置
            //Win32_PrintJob, // 打印机任务
            //Win32_TCPIPPrinterPort, // 打印机端口
            //Win32_POTSModem, // MODEM
            //Win32_POTSModemToSerialPort, // MODEM 端口
            //Win32_DesktopMonitor, // 显示器
            //Win32_DisplayConfiguration, // 显卡
            //Win32_DisplayControllerConfiguration, // 显卡设置
            //Win32_VideoController, // 显卡细节。
            //Win32_VideoSettings, // 显卡支持的显示模式。

            //// 操作系统
            //Win32_TimeZone, // 时区
            //Win32_SystemDriver, // 驱动程序
            //Win32_DiskPartition, // 磁盘分区
            //Win32_LogicalDisk, // 逻辑磁盘
            //Win32_LogicalDiskToPartition, // 逻辑磁盘所在分区及始末位置。
            //Win32_LogicalMemoryConfiguration, // 逻辑内存配置
            //Win32_PageFile, // 系统页文件信息
            //Win32_PageFileSetting, // 页文件设置
            //Win32_BootConfiguration, // 系统启动配置
            //Win32_ComputerSystem, // 计算机信息简要
            //Win32_OperatingSystem, // 操作系统信息
            //Win32_StartupCommand, // 系统自动启动程序
            //Win32_Service, // 系统安装的服务
            //Win32_Group, // 系统管理组
            //Win32_GroupUser, // 系统组帐号
            //Win32_UserAccount, // 用户帐号
            //Win32_Process, // 系统进程
            //Win32_Thread, // 系统线程
            //Win32_Share, // 共享
            //Win32_NetworkClient, // 已安装的网络客户端
            //Win32_NetworkProtocol, // 已安装的网络协议
            Win32_PnPEntity,//all device
        }
        #endregion
    }

    public class DataReceivedEventArgs : EventArgs
    {
        public string DataReceived;
        public DataReceivedEventArgs(string m_DataReceived)
        {
            this.DataReceived = m_DataReceived;
        }
    }

    public delegate void DataReceivedEventHandler(DataReceivedEventArgs e);


    /// <summary>
    /// 串口数据位列表（5,6,7,8）
    /// </summary>
    public enum SerialPortDatabits : int
    {
        FiveBits = 5,
        SixBits = 6,
        SeventBits = 7,
        EightBits = 8
    }

    /// <summary>
    /// 串口波特率列表。
    /// 75,110,150,300,600,1200,2400,4800,9600,14400,19200,28800,38400,56000,57600,
    /// 115200,128000,230400,256000
    /// </summary>
    public enum SerialPortBaudRates : int
    {
        BaudRate_75 = 75,
        BaudRate_110 = 110,
        BaudRate_150 = 150,
        BaudRate_300 = 300,
        BaudRate_600 = 600,
        BaudRate_1200 = 1200,
        BaudRate_2400 = 2400,
        BaudRate_4800 = 4800,
        BaudRate_9600 = 9600,
        BaudRate_14400 = 14400,
        BaudRate_19200 = 19200,
        BaudRate_28800 = 28800,
        BaudRate_38400 = 38400,
        BaudRate_56000 = 56000,
        BaudRate_57600 = 57600,
        BaudRate_115200 = 115200,
        BaudRate_128000 = 128000,
        BaudRate_230400 = 230400,
        BaudRate_256000 = 256000
    }



}
