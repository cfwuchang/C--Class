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
        /// �����ļ����������
        /// </summary>
        private RandomNumberGenerator rand = new RNGCryptoServiceProvider();

        /// <summary>
        /// �쳣������
        /// </summary>
        public class CryptoHelpException : ApplicationException
        {
            public CryptoHelpException(string msg) : base(msg) { }
        }

        /// <summary>
        /// ����ָ�����ȵ����Byte����
        /// </summary>
        /// <param name="count">Byte���鳤��</param>
        /// <returns>���Byte����</returns>
        private byte[] GenerateRandomBytes(int count)
        {
            byte[] bytes = new byte[count];
            rand.GetBytes(bytes);
            return bytes;
        }

        /// <summary>
        /// ��������Byte�����Ƿ���ͬ
        /// </summary>
        /// <param name="b1">Byte����</param>
        /// <param name="b2">Byte����</param>
        /// <returns>true�����</returns>
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
        /// �������ܶ�����ִ��Rijndael�㷨
        /// </summary>
        /// <param name="password">����</param>
        /// <param name="salt"></param>
        /// <returns>���ܶ���</returns>
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
        /// �����ļ�
        /// </summary>
        /// <param name="inFile">�������ļ�</param>
        /// <param name="outFile">���ܺ������ļ�</param>
        /// <param name="password">��������</param>
        public void EncryptFile(string inFile, string outFile, string password)
        {
            using (FileStream fin = File.OpenRead(inFile),
                fout = File.OpenWrite(outFile))
            {
                long lSize = fin.Length; // �����ļ�����
                int size = (int)lSize;
                byte[] bytes = new byte[BUFFER_SIZE]; // ����
                int read = -1; // �����ļ���ȡ����
                int value = 0;

                // ��ȡIV��salt
                byte[] IV = GenerateRandomBytes(16);
                byte[] salt = GenerateRandomBytes(16);

                // �������ܶ���
                SymmetricAlgorithm sma = CreateRijndael(password, salt);
                sma.IV = IV;

                // ������ļ���ʼ����д��IV��salt
                fout.Write(IV, 0, IV.Length);
                fout.Write(salt, 0, salt.Length);

                // ����ɢ�м���
                HashAlgorithm hasher = SHA256.Create();
                using (CryptoStream cout = new CryptoStream(fout, sma.CreateEncryptor(), CryptoStreamMode.Write),
                    chash = new CryptoStream(Stream.Null, hasher, CryptoStreamMode.Write))
                {
                    BinaryWriter bw = new BinaryWriter(cout);
                    bw.Write(lSize);

                    bw.Write(FC_TAG);

                    // ��д�ֽڿ鵽������������
                    while ((read = fin.Read(bytes, 0, bytes.Length)) != 0)
                    {
                        cout.Write(bytes, 0, read);
                        chash.Write(bytes, 0, read);
                        value += read;
                    }
                    // �رռ�����
                    chash.Flush();
                    chash.Close();

                    // ��ȡɢ��
                    byte[] hash = hasher.Hash;

                    // �����ļ�д��ɢ��
                    cout.Write(hash, 0, hash.Length);

                    // �ر��ļ���
                    cout.Flush();
                    cout.Close();
                }
            }
        }

        /// <summary>
        /// �����ļ�
        /// </summary>
        /// <param name="inFile">�������ļ�</param>
        /// <param name="outFile">���ܺ�����ļ�</param>
        /// <param name="password">��������</param>
        public void DecryptFile(string inFile, string outFile, string password)
        {
            // �������ļ���
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

                // ����ɢ�ж���, У���ļ�
                HashAlgorithm hasher = SHA256.Create();

                using (CryptoStream cin = new CryptoStream(fin, sma.CreateDecryptor(), CryptoStreamMode.Read),
                    chash = new CryptoStream(Stream.Null, hasher, CryptoStreamMode.Write))
                {
                    // ��ȡ�ļ�����
                    BinaryReader br = new BinaryReader(cin);
                    lSize = br.ReadInt64();
                    ulong tag = br.ReadUInt64();

                    if (FC_TAG != tag)
                        throw new CryptoHelpException("�ļ����ƻ�");

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

                    // ��ȡ�ȽϺ;ɵ�ɢ�ж���
                    byte[] oldHash = new byte[hasher.HashSize / 8];
                    read = cin.Read(oldHash, 0, oldHash.Length);
                    if ((oldHash.Length != read) || (!CheckByteArrays(oldHash, curHash)))
                        throw new CryptoHelpException("�ļ����ƻ�");
                }

                if (outValue != lSize)
                    throw new CryptoHelpException("�ļ���С��ƥ��");
            }
        }

        /// <summary>
        /// hash�ļ�
        /// </summary>
        /// <param name="path">�ļ�</param>

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
        ///�����ļ�
        ///</summary>
        ///sir ����·��
        ///generate �ļ���β
        ///�ļ����½�true ����false
        ///args ����
        ///flow txt ����false�滻�ļ�ֵ
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
                        // �滻�ļ�ֵ
                        File.WriteAllText(sir, args, Encoding.Default);
                    }
                    else
                    {
                        // ������д��
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
        /// ���ļ����´���csv�ļ�
        /// </summary>
        /// <param name="projectName">��Ŀ���ơ�Ϊ��Ŀ��������һ���ļ���</param>
        /// <param name="saveDirectoryPath">�����ļ��е�·��������Data�ļ�����</param>
        /// <param name="columnNameList">�������</param>
        public static void CreateFile(string projectName, string saveDirectoryPath, string[] columnNameList = null)
        {
            string path = "";//����csv�ļ��ĵ�·��
            string Directory_Path = saveDirectoryPath + "\\" + projectName;//��Ŀ�����ļ��е�·��

            //�ж��Ƿ������Ŀ�ļ��У����û���򴴽�
            if (Directory.Exists(Directory_Path) == false)//���������
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
        /// ɾ���ļ��м��ļ�
        /// </summary>
        ///  <param name="path">�ļ���·��</param>
        public static void deleteFile(string path)
        {
            Directory.GetFiles(path).ToList().ForEach(a => File.Delete(a));
            Directory.Delete(path, true);
        }


        /// <summary>
        /// ����Xml�ļ�
        /// </summary>
        /// <param name="RootNode">���ڵ�</param>
        /// <param name="newNode">���ڵ�</param>
        /// <param name="dictionary">�ӽڵ㼰����</param>
        /// <param name="dir">��ַ</param>
        public void CreateXmlFile(string RootNode, string newNode, Dictionary<string, string> dictionary, string dir)
        {
            XmlDocument xmlDoc = new XmlDocument();
            //�������������ڵ�  
            XmlNode node = xmlDoc.CreateXmlDeclaration("1.0", "utf-8", "");
            xmlDoc.AppendChild(node);
            //����Xml���ڵ�  
            XmlNode root = xmlDoc.CreateElement(RootNode);
            xmlDoc.AppendChild(root);
            // ����Xml���ڵ� 
            XmlNode root1 = xmlDoc.CreateElement(newNode);
            root.AppendChild(root1);
            foreach (var item in dictionary)
            {
                //�����ӽڵ�
                CreateNode(xmlDoc, root1, item.Key, item.Value);
            }
            //���ļ����浽ָ��λ��
            xmlDoc.Save(dir + ".xml");
        }

        /// <summary>    
        /// �����ڵ�    
        /// </summary>    
        /// <param name="xmlDoc">xml�ĵ�</param>    
        /// <param name="parentNode">Xml���ڵ�</param>    
        /// <param name="name">�ڵ���</param>    
        /// <param name="value">�ڵ�ֵ</param>    
        ///   
        public void CreateNode(XmlDocument xmlDoc, XmlNode parentNode, string name, string value)
        {
            //������ӦXml�ڵ�Ԫ��
            XmlNode node = xmlDoc.CreateNode(XmlNodeType.Element, name, null);
            node.InnerText = value;
            parentNode.AppendChild(node);
        }

        /// <summary>
        /// �������ڵ�
        /// </summary>
        private void AppparNode(string dir, string root, string parentNode)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(dir);//����Xml�ļ�
            XmlNode node = xmlDoc.CreateNode(XmlNodeType.Element, root, null);
            XmlNode xmlnode = xmlDoc.SelectSingleNode(parentNode);//ѡ��Ҫ����ӽڵ��book�ڵ�
            xmlnode.AppendChild(node);
        }

        /// <summary>
        /// �����ӽڵ�
        /// </summary>
        /// <param name="dir">��ַ</param> 
        /// <param name="dictionary">�ӽڵ�����</param>
        /// <param name="oldnode">���ڵ�</param>
        private void AppendNode(string dir, string oldnode, Dictionary<string, string> dictionary)
        {
            try
            {
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(dir);//����Xml�ļ�
                XmlNode root = xmlDoc.SelectSingleNode(oldnode);//ѡ��Ҫ����ӽڵ��book�ڵ�
                //����һ���µ�Xml�ڵ�Ԫ��
                foreach (var item in dictionary)
                {
                    //�����ӽڵ�
                    XmlNode node = xmlDoc.CreateNode(XmlNodeType.Element, item.Key, null);
                    node.InnerText = item.Value;
                    root.AppendChild(node);//��������item�ӽڵ���ӵ�items�ڵ��β��
                }
                //���ļ����浽ָ��λ��
                xmlDoc.Save(dir);
            }
            catch (Exception)
            {

            }

        }

        /// <summary>
        /// �޸�xml�ļ�
        /// </summary>
        /// <param name="dir">��ַ</param> 
        /// <param name="dictionary">�ӽڵ�����</param>
        /// <param name="oldnode">���ڵ�</param>
        private void UpdateXml(string dir, string oldnode, Dictionary<string, string> dictionary)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(dir);//����Xml�ļ�
            XmlNode xns = xmlDoc.SelectSingleNode(oldnode);//����Ҫ�޸ĵĽڵ�
            XmlNodeList xmlNodeList = xns.ChildNodes;//ȡ��book�ڵ������е��ӽڵ�

            foreach (XmlNode xmlNode in xmlNodeList)
            {
                foreach (var item in dictionary)
                {
                    XmlElement xmlElement = (XmlElement)xmlNode;//���ڵ�ת��һ������
                    if (xmlElement.Name == item.Key)//�жϸ��ӽڵ��Ƿ���Ҫ���ҵĽڵ�
                    {
                        xmlElement.InnerText = item.Value;//������ֵ
                        break;
                    }
                }
            }
            xmlDoc.Save(dir);//�����޸ĵ�Xml�ļ�����
        }

        /// <summary>
        /// ɾ��ָ��xml�ļ��ڵ�
        /// </summary>
        /// <param name="dir">��ַ</param> 
        /// <param name="data">�ӽڵ�</param>
        /// <param name="oldnode">���ڵ�</param>
        /// <param name="boo">�Ƿ�ɾ��ȫ��</param>

        private void ClearDataXmlNode(string dir, string oldnode, string data, bool boo)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(dir);//����Xml�ļ�
            XmlNode xns = xmlDoc.SelectSingleNode(oldnode);//����Ҫɾ���ĸ��ڵ�
            if (boo)
            {
                // ���author�ڵ��µ�����
                XmlNodeList xmlNodeList = xns.ChildNodes;//ȡ��book�ڵ������е��ӽڵ�
                foreach (XmlNode xmlNode in xmlNodeList)
                {
                    XmlElement xmlElement = (XmlElement)xmlNode;//���ڵ�ת��һ������
                    if (xmlElement.Name == data)//�жϸ��ӽڵ��Ƿ���Ҫ���ҵĽڵ�
                    {
                        //���author�ڵ��µ�����
                        xmlElement.RemoveAll();//ɾ���ýڵ��ȫ������
                    }
                }
            }
            else
            {
                var delNode = xmlDoc.SelectSingleNode(oldnode + data);
                xns.RemoveChild(delNode);
            }
            xmlDoc.Save(dir);//����������Xml�ļ�����
        }


        /// <summary>
        /// ���ؼ����㷨������
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
        /// XML���ܷ���
        /// </summary>
        /// <param name="path">��ַ</param>
        /// <param name="node">���ڵ�</param>
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
        /// XML���ܷ���
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
