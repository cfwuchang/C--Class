using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.IO;
using System.Security.Cryptography;
using System.Xml;
using System.Security.Cryptography.Xml;

namespace file_encryption_decryption
{
    public class file
    {

        private const ulong FC_TAG = 0xFC010203040506CF;

        private const int BUFFER_SIZE = 128 * 1024;
        /// <summary>
        /// 加密文件随机数生成
        /// </summary>
        private RandomNumberGenerator rand = new RNGCryptoServiceProvider();

        /// <summary>
        /// 异常处理类
        /// </summary>
        public class CryptoHelpException : ApplicationException
        {
            public CryptoHelpException(string msg) : base(msg) { }
        }

        /// <summary>
        /// 生成指定长度的随机Byte数组
        /// </summary>
        /// <param name="count">Byte数组长度</param>
        /// <returns>随机Byte数组</returns>
        private byte[] GenerateRandomBytes(int count)
        {
            byte[] bytes = new byte[count];
            rand.GetBytes(bytes);
            return bytes;
        }

        /// <summary>
        /// 检验两个Byte数组是否相同
        /// </summary>
        /// <param name="b1">Byte数组</param>
        /// <param name="b2">Byte数组</param>
        /// <returns>true－相等</returns>
        private bool CheckByteArrays(byte[] b1, byte[] b2)
        {
            if (b1.Length == b2.Length)
            {
                for (int i = 0; i < b1.Length; ++i)
                {
                    if (b1[i] != b2[i])
                        return false;
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// 创建加密对象以执行Rijndael算法
        /// </summary>
        /// <param name="password">密码</param>
        /// <param name="salt"></param>
        /// <returns>加密对象</returns>
        private SymmetricAlgorithm CreateRijndael(string password, byte[] salt)
        {
            PasswordDeriveBytes pdb = new PasswordDeriveBytes(password, salt, "SHA256", 1000);
            SymmetricAlgorithm sma = Rijndael.Create();
            sma.KeySize = 256;
            sma.Key = pdb.GetBytes(32);
            sma.Padding = PaddingMode.PKCS7;
            return sma;
        }

        /// <summary>
        /// 加密文件
        /// </summary>
        /// <param name="inFile">待加密文件</param>
        /// <param name="outFile">加密后输入文件</param>
        /// <param name="password">加密密码</param>
        public void EncryptFile(string inFile, string outFile, string password)
        {
            using (FileStream fin = File.OpenRead(inFile),
                fout = File.OpenWrite(outFile))
            {
                long lSize = fin.Length; // 输入文件长度
                int size = (int)lSize;
                byte[] bytes = new byte[BUFFER_SIZE]; // 缓存
                int read = -1; // 输入文件读取数量
                int value = 0;

                // 获取IV和salt
                byte[] IV = GenerateRandomBytes(16);
                byte[] salt = GenerateRandomBytes(16);

                // 创建加密对象
                SymmetricAlgorithm sma = CreateRijndael(password, salt);
                sma.IV = IV;

                // 在输出文件开始部分写入IV和salt
                fout.Write(IV, 0, IV.Length);
                fout.Write(salt, 0, salt.Length);

                // 创建散列加密
                HashAlgorithm hasher = SHA256.Create();
                using (CryptoStream cout = new CryptoStream(fout, sma.CreateEncryptor(), CryptoStreamMode.Write),
                    chash = new CryptoStream(Stream.Null, hasher, CryptoStreamMode.Write))
                {
                    BinaryWriter bw = new BinaryWriter(cout);
                    bw.Write(lSize);

                    bw.Write(FC_TAG);

                    // 读写字节块到加密流缓冲区
                    while ((read = fin.Read(bytes, 0, bytes.Length)) != 0)
                    {
                        cout.Write(bytes, 0, read);
                        chash.Write(bytes, 0, read);
                        value += read;
                    }
                    // 关闭加密流
                    chash.Flush();
                    chash.Close();

                    // 读取散列
                    byte[] hash = hasher.Hash;

                    // 输入文件写入散列
                    cout.Write(hash, 0, hash.Length);

                    // 关闭文件流
                    cout.Flush();
                    cout.Close();
                }
            }
        }

        /// <summary>
        /// 解密文件
        /// </summary>
        /// <param name="inFile">待解密文件</param>
        /// <param name="outFile">解密后输出文件</param>
        /// <param name="password">解密密码</param>
        public void DecryptFile(string inFile, string outFile, string password)
        {
            // 创建打开文件流
            using (FileStream fin = File.OpenRead(inFile),
                fout = File.OpenWrite(outFile))
            {
                int size = (int)fin.Length;
                byte[] bytes = new byte[BUFFER_SIZE];
                int read = -1;
                int value = 0;
                int outValue = 0;

                byte[] IV = new byte[16];
                fin.Read(IV, 0, 16);
                byte[] salt = new byte[16];
                fin.Read(salt, 0, 16);

                SymmetricAlgorithm sma = CreateRijndael(password, salt);
                sma.IV = IV;

                value = 32;
                long lSize = -1;

                // 创建散列对象, 校验文件
                HashAlgorithm hasher = SHA256.Create();

                using (CryptoStream cin = new CryptoStream(fin, sma.CreateDecryptor(), CryptoStreamMode.Read),
                    chash = new CryptoStream(Stream.Null, hasher, CryptoStreamMode.Write))
                {
                    // 读取文件长度
                    BinaryReader br = new BinaryReader(cin);
                    lSize = br.ReadInt64();
                    ulong tag = br.ReadUInt64();

                    if (FC_TAG != tag)
                        throw new CryptoHelpException("文件被破坏");

                    long numReads = lSize / BUFFER_SIZE;

                    long slack = (long)lSize % BUFFER_SIZE;

                    for (int i = 0; i < numReads; ++i)
                    {
                        read = cin.Read(bytes, 0, bytes.Length);
                        fout.Write(bytes, 0, read);
                        chash.Write(bytes, 0, read);
                        value += read;
                        outValue += read;
                    }

                    if (slack > 0)
                    {
                        read = cin.Read(bytes, 0, (int)slack);
                        fout.Write(bytes, 0, read);
                        chash.Write(bytes, 0, read);
                        value += read;
                        outValue += read;
                    }

                    chash.Flush();
                    chash.Close();

                    fout.Flush();
                    fout.Close();

                    byte[] curHash = hasher.Hash;

                    // 获取比较和旧的散列对象
                    byte[] oldHash = new byte[hasher.HashSize / 8];
                    read = cin.Read(oldHash, 0, oldHash.Length);
                    if ((oldHash.Length != read) || (!CheckByteArrays(oldHash, curHash)))
                        throw new CryptoHelpException("文件被破坏");
                }

                if (outValue != lSize)
                    throw new CryptoHelpException("文件大小不匹配");
            }
        }

        /// <summary>
        /// hash文件
        /// </summary>
        /// <param name="path">文件</param>

        public string GetHash(string path)
        {
            //var hash = SHA256.Create();
            //var hash = MD5.Create();
            var hash = SHA1.Create();
            var stream = new FileStream(path, FileMode.Open);
            byte[] hashByte = hash.ComputeHash(stream);
            stream.Close();
            return BitConverter.ToString(hashByte).Replace("-", "");
        }

        ///<summary>
        ///生成文件
        ///</summary>
        ///sir 数据路径
        ///generate 文件结尾
        ///文件不新建true 覆盖false
        ///args 数据
        ///flow txt 覆盖false替换文件值
        public bool GenerateFile(string sir, string generate, bool bool1, string args, bool flow)
        {
            try
            {
                string SDir = Path.Combine(sir + generate);
                FileMode fm;
                if (bool1)
                {
                    fm = FileMode.Append;
                }
                else
                {
                    fm = FileMode.Create;
                }

                FileStream fs = new FileStream(SDir, fm, FileAccess.Write);
                System.IO.StreamWriter sw = new System.IO.StreamWriter(fs);

                if (generate == ".json")
                {
                    File.WriteAllText(sir + "/data.json", args);
                }
                // flow
                else if (generate == ".txt")
                {
                    if (!flow)
                    {
                        // 替换文件值
                        File.WriteAllText(sir, args, Encoding.Default);
                    }
                    else
                    {
                        // 流数据写入
                        sw.WriteLine(args);
                    }
                }
                sw.Close();
                fs.Close();

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 在文件夹下创建csv文件
        /// </summary>
        /// <param name="projectName">项目名称。为项目单独创建一个文件夹</param>
        /// <param name="saveDirectoryPath">保存文件夹的路径。例如Data文件夹下</param>
        /// <param name="columnNameList">标题队列</param>
        public static void CreateFile(string projectName, string saveDirectoryPath, string[] columnNameList = null)
        {
            string path = "";//保存csv文件的的路径
            string Directory_Path = saveDirectoryPath + "\\" + projectName;//项目所在文件夹的路径

            //判断是否存在项目文件夹，如果没有则创建
            if (Directory.Exists(Directory_Path) == false)//如果不存在
            {
                Directory.CreateDirectory(Directory_Path);
            }

            path = Directory_Path + "\\" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".csv";
            if (!System.IO.File.Exists(path))
            {
                FileStream stream = System.IO.File.Create(path);
                stream.Close();
                stream.Dispose();
                using (StreamWriter writer = new StreamWriter(path, true))
                {
                    string WriteRecord = "Time,";
                    for (int i = 0; i < columnNameList.Length; i++)
                    {
                        if (i != columnNameList.Length - 1)
                            WriteRecord += columnNameList[i] + ",";
                        else
                            WriteRecord += columnNameList[i];
                    }

                    writer.WriteLine(WriteRecord, Encoding.UTF8);
                    writer.Flush();
                    writer.Close();
                }
            }
        }
        /// <summary>
        /// 删除文件夹及文件
        /// </summary>
        ///  <param name="path">文件夹路径</param>
        public static void deleteFile(string path)
        {
            Directory.GetFiles(path).ToList().ForEach(a => File.Delete(a));
            Directory.Delete(path, true);
        }


        /// <summary>
        /// 创建Xml文件
        /// </summary>
        /// <param name="RootNode">根节点</param>
        /// <param name="newNode">父节点</param>
        /// <param name="dictionary">子节点及数据</param>
        /// <param name="dir">地址</param>
        public void CreateXmlFile(string RootNode, string newNode, Dictionary<string, string> dictionary, string dir)
        {
            XmlDocument xmlDoc = new XmlDocument();
            //创建类型声明节点  
            XmlNode node = xmlDoc.CreateXmlDeclaration("1.0", "utf-8", "");
            xmlDoc.AppendChild(node);
            //创建Xml根节点  
            XmlNode root = xmlDoc.CreateElement(RootNode);
            xmlDoc.AppendChild(root);
            // 创建Xml父节点 
            XmlNode root1 = xmlDoc.CreateElement(newNode);
            root.AppendChild(root1);
            foreach (var item in dictionary)
            {
                //创建子节点
                CreateNode(xmlDoc, root1, item.Key, item.Value);
            }
            //将文件保存到指定位置
            xmlDoc.Save(dir + ".xml");
        }

        /// <summary>    
        /// 创建节点    
        /// </summary>    
        /// <param name="xmlDoc">xml文档</param>    
        /// <param name="parentNode">Xml父节点</param>    
        /// <param name="name">节点名</param>    
        /// <param name="value">节点值</param>    
        ///   
        public void CreateNode(XmlDocument xmlDoc, XmlNode parentNode, string name, string value)
        {
            //创建对应Xml节点元素
            XmlNode node = xmlDoc.CreateNode(XmlNodeType.Element, name, null);
            node.InnerText = value;
            parentNode.AppendChild(node);
        }

        /// <summary>
        /// 创建父节点
        /// </summary>
        private void AppparNode(string dir, string root, string parentNode)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(dir);//加载Xml文件
            XmlNode node = xmlDoc.CreateNode(XmlNodeType.Element, root, null);
            XmlNode xmlnode = xmlDoc.SelectSingleNode(parentNode);//选择要添加子节点的book节点
            xmlnode.AppendChild(node);
        }

        /// <summary>
        /// 创建子节点
        /// </summary>
        /// <param name="dir">地址</param> 
        /// <param name="dictionary">子节点数据</param>
        /// <param name="oldnode">父节点</param>
        private void AppendNode(string dir, string oldnode, Dictionary<string, string> dictionary)
        {
            try
            {
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(dir);//加载Xml文件
                XmlNode root = xmlDoc.SelectSingleNode(oldnode);//选择要添加子节点的book节点
                //创建一个新的Xml节点元素
                foreach (var item in dictionary)
                {
                    //创建子节点
                    XmlNode node = xmlDoc.CreateNode(XmlNodeType.Element, item.Key, null);
                    node.InnerText = item.Value;
                    root.AppendChild(node);//将创建的item子节点添加到items节点的尾部
                }
                //将文件保存到指定位置
                xmlDoc.Save(dir);
            }
            catch (Exception)
            {

            }

        }

        /// <summary>
        /// 修改xml文件
        /// </summary>
        /// <param name="dir">地址</param> 
        /// <param name="dictionary">子节点数据</param>
        /// <param name="oldnode">父节点</param>
        private void UpdateXml(string dir, string oldnode, Dictionary<string, string> dictionary)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(dir);//加载Xml文件
            XmlNode xns = xmlDoc.SelectSingleNode(oldnode);//查找要修改的节点
            XmlNodeList xmlNodeList = xns.ChildNodes;//取出book节点下所有的子节点

            foreach (XmlNode xmlNode in xmlNodeList)
            {
                foreach (var item in dictionary)
                {
                    XmlElement xmlElement = (XmlElement)xmlNode;//将节点转换一下类型
                    if (xmlElement.Name == item.Key)//判断该子节点是否是要查找的节点
                    {
                        xmlElement.InnerText = item.Value;//设置新值
                        break;
                    }
                }
            }
            xmlDoc.Save(dir);//保存修改的Xml文件内容
        }

        /// <summary>
        /// 删除指定xml文件节点
        /// </summary>
        /// <param name="dir">地址</param> 
        /// <param name="data">子节点</param>
        /// <param name="oldnode">父节点</param>
        /// <param name="boo">是否删除全部</param>

        private void ClearDataXmlNode(string dir, string oldnode, string data, bool boo)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(dir);//加载Xml文件
            XmlNode xns = xmlDoc.SelectSingleNode(oldnode);//查找要删除的根节点
            if (boo)
            {
                // 清空author节点下的数据
                XmlNodeList xmlNodeList = xns.ChildNodes;//取出book节点下所有的子节点
                foreach (XmlNode xmlNode in xmlNodeList)
                {
                    XmlElement xmlElement = (XmlElement)xmlNode;//将节点转换一下类型
                    if (xmlElement.Name == data)//判断该子节点是否是要查找的节点
                    {
                        //清空author节点下的数据
                        xmlElement.RemoveAll();//删除该节点的全部内容
                    }
                }
            }
            else
            {
                var delNode = xmlDoc.SelectSingleNode(oldnode + data);
                xns.RemoveChild(delNode);
            }
            xmlDoc.Save(dir);//保存操作后的Xml文件内容
        }


        /// <summary>
        /// 返回加密算法及类型
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        static SymmetricAlgorithm Create(out string method)
        {
            RijndaelManaged TDESKey = new RijndaelManaged();
            TDESKey.Key = Encoding.Unicode.GetBytes("0123456789012345");
            TDESKey.Mode = CipherMode.ECB;
            TDESKey.Padding = PaddingMode.PKCS7;
            switch (TDESKey.KeySize)
            {
                case 128: method = EncryptedXml.XmlEncAES128Url; break;
                case 256: method = EncryptedXml.XmlEncAES256Url; break;
                case 192: method = EncryptedXml.XmlEncAES192Url; break;
                default: method = EncryptedXml.XmlEncAES128Url; break;
            }
            return TDESKey;
        }

        /// <summary>
        /// XML加密方法
        /// </summary>
        /// <param name="path">地址</param>
        /// <param name="node">父节点</param>
        /// 
        public void Encrypt(string path, string node)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(path);
            XmlElement element = (doc.FirstChild is XmlDeclaration ? doc.FirstChild.NextSibling : doc.FirstChild) as XmlElement;
            if (element == null || element.Name != node)
                return;
            EncryptedXml eXML = new EncryptedXml(doc);
            string method;
            byte[] outPut = eXML.EncryptData(element, Create(out method), false);
            EncryptedData eData = new EncryptedData()
            {
                Type = EncryptedXml.XmlEncElementUrl,
                EncryptionMethod = new EncryptionMethod(method),
                CipherData = new CipherData()
                {
                    CipherValue = outPut,
                }
            };
            EncryptedXml.ReplaceElement(element, eData, false);
            doc.Save(path);
        }

        /// <summary>
        /// XML解密方法
        /// </summary>
        /// <param name="path"></param>

        public void Decrypt(string path)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(path);
            XmlElement element = doc.GetElementsByTagName("EncryptedData")[0] as XmlElement;
            if (element == null)
                return;
            EncryptedData eData = new EncryptedData();
            eData.LoadXml(element);
            EncryptedXml eXML = new EncryptedXml();
            string method;
            byte[] outPut = eXML.DecryptData(eData, Create(out method));
            eXML.ReplaceData(element, outPut);
            doc.Save(path);
        }
    }
}
