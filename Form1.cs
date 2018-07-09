using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO.Ports;
using System.Text.RegularExpressions;

namespace SerialportSample
{
    public partial class SerialportSampleForm : Form
    {
        private SerialPort comm = new SerialPort();
        private StringBuilder builder = new StringBuilder();//避免在事件处理方法中反复的创建，定义到外面。
        private long received_count = 0;//接收计数

        // LiuJiaJun - 增加一个变量进行单组数据是否接收完全的判断
        private StringBuilder builder_data = new StringBuilder();
        private long batch_received_count = 0;

        private long send_count = 0;//发送计数

        private delegate void DelegateCallBackData(string data);
        //Mouse check drawing
        private DelegateCallBackData delegateData = null;

        public SerialportSampleForm()
        {
            InitializeComponent();
        }

        //窗体初始化
        private void Form1_Load(object sender, EventArgs e)
        {
            // Liu - builder 清空，保险起见
            builder_data.Clear();

            string str = "0xEE";
            string str1 = "0x16";
            int i = Convert.ToInt32(str, 16);
            int ii = Convert.ToInt32(str1, 16);
            string temp = string.Format("{0:X}", i);
            string temp1 = string.Format("{0:X}", ii);
            //初始化下拉串口名称列表框
            string[] ports = SerialPort.GetPortNames();
            Array.Sort(ports);
            comboPortName.Items.AddRange(ports);
            comboPortName.SelectedIndex = comboPortName.Items.Count > 0 ? 0 : -1;
            comboBaudrate.SelectedIndex = comboBaudrate.Items.IndexOf("9600");

            //初始化SerialPort对象
            comm.NewLine = "\r\n";
            comm.RtsEnable = true;//根据实际情况吧。

            //添加事件注册
            comm.DataReceived += comm_DataReceived;

            delegateData += ReceivedData;
        }

        private string HexStringToString(string hs, Encoding encode)
        {
            string[] chars = hs.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            byte[] b = new byte[chars.Length];
            for (int i = 0; i < chars.Length; i++)
            {
                b[i] = Convert.ToByte(chars[i], 16);
            }
            return encode.GetString(b);
        }

        private void ReceivedData(string data)
        {
            /*
            string[] sArray;
            //接收数据是否存在01 03 02,,,,,,,,,,,,,,,,,,,,,,010300000001840A
            if (data.Contains("01 03 02"))
            {
                //得到01 03 02以外字串
                sArray = data.Split(new string[] { "01 03 02" }, StringSplitOptions.RemoveEmptyEntries);
                //如果获取字串数据大于0 说明得到01 03 02以外字串成功
                if (sArray.Length > 0)
                {
                    //截取4位有效字串，因为接收数据两位之间包含一个空格，因此设置6
                    if (sArray[0].Length > 6)
                    sArray[0] = sArray[0].Remove(6);
                    //去掉字串中的空格
                    string temp = sArray[0].Replace(" ","");
                    
                    double nValue = 0;
                    //数据解析
                    for (int i = 0; i < temp.Length; i++)
                    {
                        string a = temp[i].ToString();
                        int nTemp = Convert.ToInt32(a,16);
                        nValue += nTemp * Math.Pow(16, temp.Length - i -1);
                    }
                    //毫安算法
                    double dMA = (nValue * 20) / 10000;
                    //高度算法
                    double dHigh = 18.0645 * dMA - 220.1225;

                    SetChartData(dHigh);
                }
            }
            */
            //================DengYan 代码==========================
            //================Liu 代码=========================
            //Step 1. 判断前三个是否为固定开头数据
            byte[] batchdata = System.Text.Encoding.Default.GetBytes(data);
            if (batchdata[0] != 0x01)
            {
                // 报错-进入纠错机制
                SetChartData(-100.0);
                return;
            }
            if (batchdata[1] != 0x03)
            {
                // 报错-进入纠错机制
                SetChartData(-100.0);
                return;
            }
            if (batchdata[2] != 0x02)
            {
                // 报错-进入纠错机制
                SetChartData(-100.0);
                return;
            }

            // 文件头检验通过-解析数据
            UInt32 laser_data;
            laser_data = batchdata[3];
            laser_data = laser_data << 8;
            laser_data = laser_data + batchdata[4];

            // 毫安值：
            double dmA = (laser_data * 20.0) / 10000.0;
            // 高度：
            double dHeight = 600 - dmA * 50;
            SetChartData(dHeight);

        }
        private int nRow = 1;
        private delegate void DelegateCChart(double data);
        private void SetChartData(double data)
        {
            try
            {
                if (chart1.InvokeRequired)
                {
                    DelegateCChart md = new DelegateCChart(SetChartData);
                    this.Invoke(md, new object[] { data });
                }
                else
                {
                    chart1.Series[0].Points.AddXY(nRow, data);
                    nRow++;
                }
                
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(ex.Message.ToString());
            }
        }

        void comm_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            int n = comm.BytesToRead;//先记录下来，避免某种原因，人为的原因，操作几次之间时间长，缓存不一致
            byte[] buf = new byte[n];//声明一个临时数组存储当前来的串口数据
            received_count += n;//增加接收计数
            comm.Read(buf, 0, n);//读取缓冲数据

            // Liu-计数判断是否收够了一组
            batch_received_count += n;
            // Liu-判断是否够 7 bytes （目前激光测距仪肯定是7bytes，如果作为其它多路信号接收，不能这么一概而论）
            if (batch_received_count < 7)
            {
                foreach (byte b in buf)
                {
                    builder_data.Append(b); // 直接放进来
                }
            }
            else if (batch_received_count == 7)
            {
                foreach (byte b in buf)
                {
                    builder_data.Append(b); // 直接放进来
                }
                // 可以进行解析
                delegateData(builder_data.ToString());
                batch_received_count = 0;
                delegateData(builder_data.ToString());
                builder_data.Clear();
            }

            

            builder.Clear();//清除字符串构造器的内容
            //因为要访问ui资源，所以需要使用invoke方式同步ui。
            this.Invoke((EventHandler)(delegate
            {
                //判断是否是显示为16进制
                if (checkBoxHexView.Checked)
                {
                    //依次的拼接出16进制字符串
                    foreach (byte b in buf)
                    {
                        builder.Append(b.ToString("X2") + " ");
                    }
                }
                else
                {
                    //直接按ASCII规则转换成字符串
                    builder.Append(Encoding.ASCII.GetString(buf));
                }

                
                //追加的形式添加到文本框末端，并滚动到最后。
                this.txGet.AppendText(builder.ToString());
                //修改接收计数
                labelGetCount.Text = "Get:" + received_count.ToString();
            }));
        }
       
        private void buttonOpenClose_Click(object sender, EventArgs e)
        {
            //根据当前串口对象，来判断操作
            if (comm.IsOpen)
            {
                //打开时点击，则关闭串口
                comm.Close();
            }
            else
            {
                //关闭时点击，则设置好端口，波特率后打开
                comm.PortName = comboPortName.Text;
                comm.BaudRate = int.Parse(comboBaudrate.Text);
                try
                {
                    comm.Open();
                }
                catch(Exception ex)
                {
                    //捕获到异常信息，创建一个新的comm对象，之前的不能用了。
                    comm = new SerialPort();
                    //现实异常信息给客户。
                    MessageBox.Show(ex.Message);
                }
            }
            //设置按钮的状态
            buttonOpenClose.Text = comm.IsOpen ? "Close" : "Open";
            buttonSend.Enabled = comm.IsOpen;
        }

        //动态的修改获取文本框是否支持自动换行。
        private void checkBoxNewlineGet_CheckedChanged(object sender, EventArgs e)
        {
            txGet.WordWrap = checkBoxNewlineGet.Checked;
        }

        private void buttonSend_Click(object sender, EventArgs e)
        {
            //定义一个变量，记录发送了几个字节
            int n = 0;
            //16进制发送,,,010300000001840A
            if (checkBoxHexSend.Checked)
            {
                //我们不管规则了。如果写错了一些，我们允许的，只用正则得到有效的十六进制数
                MatchCollection mc = Regex.Matches(txSend.Text, @"(?i)[\da-f]{2}");
                List<byte> buf = new List<byte>();//填充到这个临时列表中
                //依次添加到列表中
                foreach (Match m in mc)
                {
                    buf.Add(byte.Parse(m.Value,System.Globalization.NumberStyles.HexNumber));
                }
                //转换列表为数组后发送
                comm.Write(buf.ToArray(), 0, buf.Count);
                //记录发送的字节数
                n = buf.Count;
            }
            else//ascii编码直接发送
            {
                //包含换行符
                if (checkBoxNewlineSend.Checked)
                {
                    comm.WriteLine(txSend.Text);
                    n = txSend.Text.Length + 2;
                }
                else//不包含换行符
                {
                    comm.Write(txSend.Text);
                    n = txSend.Text.Length;
                }
            }
            send_count += n;//累加发送字节数
            labelSendCount.Text = "Send:" + send_count.ToString();//更新界面
        }

        private void buttonReset_Click(object sender, EventArgs e)
        {
            //复位接受和发送的字节数计数器并更新界面。
            send_count = received_count = 0;
            labelGetCount.Text = "Get:0";
            labelSendCount.Text = "Send:0";
        }
    }
}
