using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using CyUSB;
using System.Diagnostics;
using System.Threading;
using System.Collections;
using System.IO;
using System.Collections.Concurrent;

using System.Windows.Forms.DataVisualization.Charting;

namespace EEG
{
    public partial class Form1 : Form
    {
        CyUSBDevice MyDevice = null;
        USBDeviceList usbDevices = null;

        Thread p_recThread = null;

        bool StartConnectOrNot = true;
        bool SaveFileBeginOrNot = true;
        bool recThread_Flag = false;
        bool Flag_recQueue_Save = false;
        bool Flag_recQueue_Save_DingZu = false;
        bool Flag__DisplayChart_ThreadingTimer__ReturnChart_Button_Click = false;//刷新chart控件
        bool test_FIR_ttt = false;
        bool[] SelectChanel = null;

        int len = len_const;//接收缓存的长度,由于此变量会由XferData回传实际读取（或接收）的值，所以不能用const修饰
        int ChangeModeFlag_Int = 0;//0为正常输出模式，1为阻抗检测模式        

        const int len_const = 512;
        const int Display_Num = 1000;
        const int perBuf_Size = len_const / 128;
        const int needreQ_Size = Display_Num / 4;
        const int bytes_Buffer_Size = 25600;//200X128
        const int TongDaoShu = 41;
        const int FIR_jieshu = 336;//使用40-55的低通滤波器
        const double coef_a = 183.6;
        const double coef_b = 5.499;
        const double vol_thd = 5.0;

        double ChangColorNum = 0;//picturebox颜色改变
        double[] FIR_h = new double[1024];//瞎J8给的数，保证滤波器阶数要小于
        double[] FlagButton_BoolArray = new double[41];//存储阻抗数值   
        double[] maxValue = new double[41];//阻抗模式中电压最大值
        double[] minValue = new double[41];//阻抗模式中电压最小值
        double[] youxiaoValue = new double[41];//有效值
        int DingZuValue = 0;//具体的组数是通过时间计算的，此变量乘1000

        ConcurrentQueue<byte[]> recQueue_test = null;
        ConcurrentQueue<byte[]> recQueue_Save = null;

        Circle_Array<byte> bytes_Buffer = null;//构建一个能存储200组数据的循环数组
        Circle_Array<double>[] erweishuzu_Circle_Array = null;
        Circle_Array<byte> shuzu_Circle_Array = null;
        Circle_Array<double>[] FIR_Circle_Array = null;

        System.Threading.Timer Timer_ThreadingTimer = null;
        //System.Threading.Timer Timer_ThreadingTimer_DingZu = null;

        Series[] serial_ch = null;
        //ComboBox[] comboBox_Array = null;

        Stopwatch stopwatch_test = null;
        Stopwatch stopwatch_DingZu = new Stopwatch();
        Stopwatch DingShiSave_StopWatch = null;
        List<double> add_List = null;
        List<byte> Trans_ByteArray = null;

        ListViewItem[] lvi_Array = null;

        string[] Chanel_Name = null;

        Button[] Array_Button = null;

        byte[][] FangDa_FenBie = null;

        public Form1()
        {
            Initialize();
            
            InitializeComponent();

            InitChart();//必须放在InitializeComponent之后初始化

            usbDevices = new USBDeviceList(CyConst.DEVICES_CYUSB);
            MyDevice = usbDevices[0x04B4, 0x0923] as CyUSBDevice;
            usbDevices.DeviceAttached += new EventHandler(usbDevices_DeviceAttached);
            usbDevices.DeviceRemoved += new EventHandler(usbDevices_DeviceRemoved);

            p_recThread = new Thread(recThread);
            p_recThread.IsBackground = true;
            p_recThread.Priority = ThreadPriority.AboveNormal;
            p_recThread.Start();//线程开启

            if (MyDevice == null)
                start_button.Enabled = false;

            Timer_ThreadingTimer = new System.Threading.Timer(new TimerCallback(DisplayChart_ThreadingTimer), null, 1000, 100);            

            Init_ChanelListView();

            for (int i = 0; i < 2; i++)
            {
                ModeChange_ComboBox.Items.Add(i.ToString());
            }

            //初始化阻抗按键数组
            Array_Button = new Button[41]{null, CH01_Button, CH02_Button, CH03_Button, CH04_Button, CH05_Button, CH06_Button, CH07_Button, CH08_Button, CH09_Button, CH10_Button,
                                                CH11_Button, CH12_Button, CH13_Button, CH14_Button, CH15_Button, CH16_Button, CH17_Button, CH18_Button, CH19_Button, CH20_Button,
                                                CH21_Button, CH22_Button, CH23_Button, CH24_Button, CH25_Button, CH26_Button, CH27_Button, CH28_Button, CH29_Button, CH30_Button,
                                                CH31_Button, CH32_Button, null,        null,        null,        null,        CH37_Button, CH38_Button, CH39_Button, CH40_Button};

            XiuGaiBeiShu_ComboBox.Items.Add("1");
            XiuGaiBeiShu_ComboBox.Items.Add("2");
            XiuGaiBeiShu_ComboBox.Items.Add("4");
            XiuGaiBeiShu_ComboBox.Items.Add("6");
            XiuGaiBeiShu_ComboBox.Items.Add("8");
            XiuGaiBeiShu_ComboBox.Items.Add("12");
            XiuGaiBeiShu_ComboBox.Items.Add("24");

            for (int i = 1; i < 7; i++)
            {
                XiuGaiCaiYang_ComboBox.Items.Add(i.ToString());
            }

            //timer1.Start();

            //comboBox_Array = new ComboBox[] { XiuGaiBeiShu1_ComboBox, XiuGaiBeiShu2_ComboBox, XiuGaiBeiShu3_ComboBox, XiuGaiBeiShu4_ComboBox, XiuGaiBeiShu5_ComboBox, XiuGaiBeiShu6_ComboBox, XiuGaiBeiShu7_ComboBox, XiuGaiBeiShu8_ComboBox };
            
            //for (int i = 0; i < 8; i++)
            //{
            //    comboBox_Array[i].Items.Add("1");
            //    comboBox_Array[i].Items.Add("2");
            //    comboBox_Array[i].Items.Add("4");
            //    comboBox_Array[i].Items.Add("6");
            //    comboBox_Array[i].Items.Add("8");
            //    comboBox_Array[i].Items.Add("12");
            //    comboBox_Array[i].Items.Add("24");
            //}
            for(int i = 0; i < 40; i++)//对于放大信号的选择应该归于这一个变量，方便日后功能的添加
            {
                XiuGaiBeiShu1_ComboBox.Items.Add((i + 1).ToString());
            }
            //for(int i = 36; i < 40; i++)
            //{
            //    XiuGaiBeiShu1_ComboBox.Items.Add((i + 1).ToString());
            //}            

            PianXuanBeiShu_ComboBox.Items.Add("1");
            PianXuanBeiShu_ComboBox.Items.Add("2");
            PianXuanBeiShu_ComboBox.Items.Add("4");
            PianXuanBeiShu_ComboBox.Items.Add("6");
            PianXuanBeiShu_ComboBox.Items.Add("8");
            PianXuanBeiShu_ComboBox.Items.Add("12");
            PianXuanBeiShu_ComboBox.Items.Add("24");

            for(int i = 0; i < 5; i++)
            {
                PianXuanPian_ComboBox.Items.Add((i + 1).ToString());
            }

            TongShiFangDa_ComboBox.Items.Add("1");
            TongShiFangDa_ComboBox.Items.Add("2");
            TongShiFangDa_ComboBox.Items.Add("4");
            TongShiFangDa_ComboBox.Items.Add("6");
            TongShiFangDa_ComboBox.Items.Add("8");
            TongShiFangDa_ComboBox.Items.Add("12");
            TongShiFangDa_ComboBox.Items.Add("24");

            //失能“定组保存”
            //DingZuSave_Button.Enabled = false;
        }

        private void Initialize()
        {
            recQueue_test = new ConcurrentQueue<byte[]>();
            recQueue_Save = new ConcurrentQueue<byte[]>();//确定队列装载类型，避免装箱拆箱

            bytes_Buffer = new Circle_Array<byte>(bytes_Buffer_Size);
            erweishuzu_Circle_Array = new Circle_Array<double>[TongDaoShu];
            for (int i = 0; i < TongDaoShu; i++)//创造41X1000的二维数组，其中每一个Circle_Array<int>对象相当于一个1000的一维数组
            {
                erweishuzu_Circle_Array[i] = new Circle_Array<double>(Display_Num);
            }
            shuzu_Circle_Array = new Circle_Array<byte>(Display_Num*128);

            serial_ch = new Series[40];
            Color[] color_Array = new Color[]{Color.Red, Color.Blue, Color.Green, Color.Gold, Color.Yellow};
            for(int i = 0; i < 40; i++)
            {
                serial_ch[i] = new Series();
                serial_ch[i].ChartType = SeriesChartType.Line;
                serial_ch[i].Color = color_Array[i%5];
                serial_ch[i].Name = (i + 1).ToString();
            }

            stopwatch_test = new Stopwatch();
            DingShiSave_StopWatch = new Stopwatch();

            //FIR_Circle_Array = new Circle_Array<double>(FIR_jieshu);
            FIR_Circle_Array = new Circle_Array<double>[TongDaoShu];//实际上由于第一路是计数，所以真正要显示的只有40路。
            for (int i = 0; i < TongDaoShu; i++ )
            {
                FIR_Circle_Array[i] = new Circle_Array<double>(FIR_jieshu);
            }


            add_List = new List<double>();
            for (int i = 0; i < 336; i++)
                add_List.Add(0);

            SelectChanel = new bool[TongDaoShu];
            for(int i = 0; i < TongDaoShu; i++)
            {
                SelectChanel[i] = false;
            }

            Chanel_Name = new string[] { "A1", "HE0", "T5", "TP7", "T3", "FT7", "F7", "HER", "O1", "P3", 
                                         "CP3", "C3", "FC3", "F3", "FP1", "FZ", "FC4", "F4", "FP2", "OZ", 
                                         "PZ", "CPZ", "CZ", "FCZ", "T4", "FT8", "F8", "VE0", "O2", "P4", 
                                         "CP4", "C4", "  ", "  ",  "  ", "  ",  "A2", "VE0", "T6", "TP8"};

            Trans_ByteArray = new List<byte>();

            FangDa_FenBie = new byte[5][];
            for(int i = 0; i < 5; i++)
            {
                FangDa_FenBie[i] = new byte[3];
                FangDa_FenBie[i][0] = 0;
                FangDa_FenBie[i][1] = 0;
                FangDa_FenBie[i][2] = 0;
            }

            
        }

        void Init_ChanelListView()
        {
            this.Chanel_ListView.Columns.Add("序号", 50, HorizontalAlignment.Left);
            this.Chanel_ListView.Columns.Add("名称", 50, HorizontalAlignment.Left);
            this.Chanel_ListView.Columns.Add("运行", 70, HorizontalAlignment.Left);
            this.Chanel_ListView.Columns.Add("放大", 70, HorizontalAlignment.Left);

            lvi_Array = new ListViewItem[TongDaoShu - 1];
            for(int i = 0; i < (TongDaoShu - 1); i++)
            {
                lvi_Array[i] = new ListViewItem();
                lvi_Array[i].ImageIndex = 0;
                lvi_Array[i].Text = (i + 1).ToString();
                lvi_Array[i].SubItems.Add(Chanel_Name[i]);
                lvi_Array[i].SubItems.Add(" ");
                lvi_Array[i].SubItems.Add("1");
            }

            this.Chanel_ListView.BeginUpdate();
            for(int i = 0; i < (TongDaoShu - 1); i++)
            {
                this.Chanel_ListView.Items.Add(lvi_Array[i]);
            }
            this.Chanel_ListView.EndUpdate();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            //关闭线程
            recThread_Flag = false;//关闭接收线程

            //关闭usb驱动
            if (usbDevices != null)
            {
                usbDevices.DeviceRemoved -= usbDevices_DeviceRemoved;
                usbDevices.DeviceAttached -= usbDevices_DeviceAttached;
                usbDevices.Dispose();
            }

            Timer_ThreadingTimer.Dispose();
            p_recThread.Abort();
            //暴力关闭
            this.Dispose();
            this.Close();
            System.Environment.Exit(0);
        }
        
        void usbDevices_DeviceAttached(object sender, EventArgs e)
        {
            USBEventArgs usbEvent = e as USBEventArgs;

            usbDevices = new USBDeviceList(CyConst.DEVICES_CYUSB);//重新连接设备
            MyDevice = usbDevices[0x04B4, 0x0923] as CyUSBDevice;
            recThread_Flag = true;//开启接收线程
            start_button.Enabled = true;//使能"连接设备"按钮
        }

        void usbDevices_DeviceRemoved(object sender, EventArgs e)
        {
            USBEventArgs usbEvent = e as USBEventArgs;

            //这三句话并不能当设备拔出的时候自动关闭线程，因为接收线程与主线程是异步的，当recThread_Flag置为false的时候
            //接收线程可能已经进入到if语句里面了。可以考虑在接收线程中添加try解决
            MyDevice = null;//销毁设备
            recThread_Flag = false;//关闭接收线程
            start_button.Enabled = false;//除能"连接设备"按钮
        }

        void recThread()
        {
            int[] shuzu_41 = new int[TongDaoShu];

            while (true)
            {
                if (recThread_Flag == true)
                {
                    try
                    {
                        byte[] buf = new byte[len];
                        MyDevice.BulkInEndPt.XferData(ref buf, ref len);

                        recQueue_test.Enqueue((byte[])buf);//为了显示，会动态增加减少
                        if (Flag_recQueue_Save == true)//普通记录数据和定时记录数据
                        {
                            recQueue_Save.Enqueue((byte[])buf);
                        }
                        DingZuSaveShuju((byte[])buf);//定组保存
                        //if (Flag_recQueue_Save_DingZu == false)
                        //    continue;
                        //if (DingZuValue > recQueue_Save.Count)
                        //{
                        //    recQueue_Save.Enqueue(buf);

                        //    //DingZuValue -= 4;
                        //}
                        ////else if (Flag_recQueue_Save_DingZu)
                        //else
                        //{
                        //    long temp_stopwatch = stopwatch_test.ElapsedMilliseconds;
                        //    System.Diagnostics.Debug.WriteLine("DingZu: {0}", temp_stopwatch);
                        //    stopwatch_test.Reset();
                            
                        //    Flag_recQueue_Save_DingZu = false;
                        //    //MessageBox.Show("定组保存", "定组保存完成");
                        //    //DingZuSave_Button.Text = "定组保存";
                        //}
                    }
                    catch
                    {
                        recThread_Flag = false;//不知道为什么，加了DingZuSaveShuju函数之后总会catch到错误，推测可能是函数内部计算部规范
                    }
                }
                else
                {
                    //Thread.Sleep(50);
                }
            }
        }

        /*
         * 输入：接收队列 暂定队列是以55 55 aa aa开始(reQ)；通道选择数组(selectchanel)
         * 输出：二维数组 Circle_Array<int>[41][1000](erwei)
         * 此函数用于波形的显示，队列的长度可控，比显示数量多一点就行。（显示数量Display_Num）
         * 已知队列中一个buf的长度为len_const，每组的长度为128，可以计算出一个队列里有多少组数据，控制数据组数稍微大于1000
         */
        void JieXiShuJu(ref ConcurrentQueue<byte[]> reQ, ref Circle_Array<double>[] erwei, bool[] selectchanel)
        {
            int reQ_Size = reQ.Count;//记录此刻队列中有多少数据

            //来多少解析多少
            for (int i = 0; i < reQ_Size; i++)
            {
                byte[] temp_bytes = null;
                int temp_int = 0;
                reQ.TryDequeue(out temp_bytes);
                if (temp_bytes == null)
                    continue;

                if (temp_bytes[0] != 0x55 || temp_bytes[1] != 0x55 || temp_bytes[2] != 0xaa || temp_bytes[3] != 0xaa)
                {
                    Action<bool> action = (x) =>
                    {
                        MessageBox.Show("error", "请重新连接下位机");
                    };
                    this.Invoke(action, true);
                }

                #region
                //for (int j = 0; j < len - 120; j++)
                //{
                //    if (temp_bytes[j] == 0x55 && temp_bytes[j + 1] == 0x55 && temp_bytes[j + 2] == 0xaa && temp_bytes[j + 3] == 0xaa)
                //    {
                //        for (int k = 0; k <= 40; k++)
                //        {
                //            temp_int = temp_bytes[k * 3 + 4 + j] * 65536 + temp_bytes[k * 3 + 5 + j] * 256 + temp_bytes[k * 3 + 6 + j];
                //            temp_int = (temp_int > 8388608) ? (temp_int - 16777216) : (temp_int);
                //            erwei[k].AddItem(temp_int);
                //        }
                //    }
                //}
                #endregion

                #region
                for (int j = 0; j < 512; j += 128)
                {
                    for (int k = 0; k < 41; k++)
                    {
                        temp_int = temp_bytes[k * 3 + 4 + j] * 65536 + temp_bytes[k * 3 + 5 + j] * 256 + temp_bytes[k * 3 + 6 + j];
                        temp_int = (temp_int > 8388608) ? (temp_int - 16777216) : (temp_int);
                        erwei[k].AddItem(temp_int);
                    }
                }
                #endregion
            }
        }

        /*
         * 输入：接收队列（reQ） 暂定队列是以55 55 aa aa开始；滤波参数（FIR_h）;通道选择数组(selectchanel)
         * 输出：滤波之后的数据（erwei）
         */
        void JieXiLvBoShuJu(ref ConcurrentQueue<byte[]> reQ, ref double[] FIR_h, ref Circle_Array<double>[] erwei, bool[] selectchanel)
        {
            int reQ_Size = reQ.Count;//记录此刻队列中有多少数据
            double sum = 0;//用于累加
            //来多少解析多少
            for (int i = 0; i < reQ_Size; i++)
            {
                byte[] temp_bytes = null;
                int temp_int = 0;
                reQ.TryDequeue(out temp_bytes);
                if (temp_bytes == null)
                    continue;

                if(temp_bytes[0] != 0x55 || temp_bytes[1] != 0x55 || temp_bytes[2] != 0xaa || temp_bytes[3] != 0xaa)
                {
                    Action<bool> action = (x) =>
                        {
                            MessageBox.Show("请重新连接下位机", "error");
                        };
                    this.Invoke(action, true);
                }

                #region
                //for (int j = 0; j < len - 120; j++)
                //{
                //    if (temp_bytes[j] == 0x55 && temp_bytes[j + 1] == 0x55 && temp_bytes[j + 2] == 0xaa && temp_bytes[j + 3] == 0xaa)
                //    {
                //        for (int k = 0; k <= 40; k++)
                //        {
                            
                            
                //            temp_int = temp_bytes[k * 3 + 4 + j] * 65536 + temp_bytes[k * 3 + 5 + j] * 256 + temp_bytes[k * 3 + 6 + j];
                //            temp_int = (temp_int > 8388608) ? (temp_int - 16777216) : (temp_int);


                //            if (k != 0 && selectchanel[k] == true)
                //            {
                //                FIR_Circle_Array[k].AddItem(temp_int);
                //                int rem_Circle_Read = FIR_Circle_Array[k].Index_Array_Save;
                //                for(int m = 0; m < FIR_jieshu; m++)
                //                {
                //                    sum += FIR_Circle_Array[k].Circle_Read(ref rem_Circle_Read) * FIR_h[FIR_jieshu - 1 - m];
                //                }
                //                erwei[k].AddItem(sum);
                //                sum = 0.0;
                //            }
                //            else if(k == 0)
                //            {
                //                erwei[0].AddItem(temp_int);
                //            }
                //        }
                //    }
                //}
                #endregion

                #region
                for(int j = 0; j < 512; j += 128)
                {
                    for(int k = 0; k < 41; k++)
                    {
                        temp_int = temp_bytes[k * 3 + 4 + j] * 65536 + temp_bytes[k * 3 + 5 + j] * 256 + temp_bytes[k * 3 + 6 + j];
                        temp_int = (temp_int > 8388608) ? (temp_int - 16777216) : (temp_int);


                        if (k != 0 && selectchanel[k] == true)
                        {
                            FIR_Circle_Array[k].AddItem(temp_int);
                            int rem_Circle_Read = FIR_Circle_Array[k].Index_Array_Save;
                            for (int m = 0; m < FIR_jieshu; m++)
                            {
                                sum += FIR_Circle_Array[k].Circle_Read(ref rem_Circle_Read) * FIR_h[FIR_jieshu - 1 - m];
                            }
                            erwei[k].AddItem(sum);
                            sum = 0.0;
                        }
                        else if (k == 0)
                        {
                            erwei[0].AddItem(temp_int);
                        }
                    }
                }
                #endregion
            }
        }

        /*
         * 输入：接收队列 暂定队列是以55 55 aa aa开始(reQ)；通道选择数组(selectchanel)
         * 输出：二维数组 Circle_Array<int>[41][1000](erwei)
         * 此函数用于波形的显示，队列的长度可控，比显示数量多一点就行。（显示数量Display_Num）
         * 已知队列中一个buf的长度为len_const，每组的长度为128，可以计算出一个队列里有多少组数据，控制数据组数稍微大于1000
         */
        void JieXiShuJu_tt(ref ConcurrentQueue<byte[]> reQ, ref Circle_Array<double>[] erwei, bool[] selectchanel)
        {
            int reQ_Size = reQ.Count;//记录此刻队列中有多少数据
            byte[] temp_ByteArray = new byte[512];
            int temp_int = 0;

            for(int i = 0; i < reQ_Size; i++)
            {
                reQ.TryDequeue(out temp_ByteArray);
                Trans_ByteArray.AddRange(temp_ByteArray);
            }

            if (Trans_ByteArray.Count < 256)
                return;

            int DaoShu_ByteArray = Trans_ByteArray.Count - 256;//倒数第二组数据的帧头
            for (int i = 0; i <= DaoShu_ByteArray; i++)
            {
                if(Trans_ByteArray[i] == 0x55 && Trans_ByteArray[i + 1] == 0x55 && Trans_ByteArray[i + 2] == 0xaa && Trans_ByteArray[i + 3] == 0xaa)
                {
                    for(int j = 0; j < 41; j++)
                    {
                        temp_int = Trans_ByteArray[j * 3 + 4 + i] * 65536 + Trans_ByteArray[j * 3 + 5 + i] * 256 + Trans_ByteArray[j * 3 + 6 + i];
                        temp_int = (temp_int > 8388608) ? (temp_int - 16777216) : (temp_int);
                        erwei[j].AddItem(temp_int);
                    }
                }
            }

            Trans_ByteArray.RemoveRange(0, DaoShu_ByteArray + 128);//将解析过的数据清除，留下一组与后面的数据衔接
        }

        /*
         * 输入：接收队列（reQ） 暂定队列是以55 55 aa aa开始；滤波参数（FIR_h）;通道选择数组(selectchanel)
         * 输出：滤波之后的数据（erwei）
         */
        void JieXiLvBoShuJu_tt(ref ConcurrentQueue<byte[]> reQ, ref double[] FIR_h, ref Circle_Array<double>[] erwei, bool[] selectchanel)
        {
            int reQ_Size = reQ.Count;//记录此刻队列中有多少数据
            byte[] temp_ByteArray = new byte[512];
            int temp_int = 0;
            double sum = 0;//用于累加

            for (int i = 0; i < reQ_Size; i++)
            {
                reQ.TryDequeue(out temp_ByteArray);
                Trans_ByteArray.AddRange(temp_ByteArray);
            }

            int DaoShu_ByteArray = Trans_ByteArray.Count - 256;//倒数第二组数据的帧头
            for (int i = 0; i <= DaoShu_ByteArray; i++)//始终留最后一组数据在list里面
            {
                if (Trans_ByteArray[i] == 0x55 && Trans_ByteArray[i + 1] == 0x55 && Trans_ByteArray[i + 2] == 0xaa && Trans_ByteArray[i + 3] == 0xaa)
                {
                    for (int j = 0; j < 41; j++)
                    {
                        temp_int = Trans_ByteArray[j * 3 + 4 + i] * 65536 + Trans_ByteArray[j * 3 + 5 + i] * 256 + Trans_ByteArray[j * 3 + 6 + i];
                        temp_int = (temp_int > 8388608) ? (temp_int - 16777216) : (temp_int);

                        if (j != 0 && selectchanel[j] == true)
                        {
                            FIR_Circle_Array[j].AddItem(temp_int);
                            int rem_Circle_Read = FIR_Circle_Array[j].Index_Array_Save;
                            for (int m = 0; m < FIR_jieshu; m++)
                            {
                                sum += FIR_Circle_Array[j].Circle_Read(ref rem_Circle_Read) * FIR_h[FIR_jieshu - 1 - m];
                            }
                            erwei[j].AddItem(sum);
                            sum = 0.0;
                        }
                        else if (j == 0)
                        {
                            erwei[0].AddItem(temp_int);
                        }
                    }
                }
            }

            Trans_ByteArray.RemoveRange(0, DaoShu_ByteArray + 128);//将解析过的数据清除，留下一组与后面的数据衔接
        }

        void fir(List<double> x, List<double> h, ref List<double> y)
        {
            double sum = 0.0;
            //y = new List<double>();
            for (int i = h.Count; i < x.Count; i++)
            {
                sum = 0.0;

                for (int j = 0; j < h.Count; j++)
                {
                    sum += h[j] * x[i - j];
                }

                y.Add(sum);
            }
        }


        /*
         * 将数据显示在chart控件上
         */
        void DisplayChart_ThreadingTimer(object state)
        {
            if (Flag__DisplayChart_ThreadingTimer__ReturnChart_Button_Click == false)
                return;


            if(ChangeModeFlag_Int == 0)//输入模式
            {
                if (test_FIR_ttt == false)
                    //JieXiShuJu(ref recQueue_test, ref erweishuzu_Circle_Array, SelectChanel);
                    JieXiShuJu_tt(ref recQueue_test, ref erweishuzu_Circle_Array, SelectChanel);
                else
                    //JieXiLvBoShuJu(ref recQueue_test, ref FIR_h, ref erweishuzu_Circle_Array, SelectChanel);
                    JieXiLvBoShuJu_tt(ref recQueue_test, ref FIR_h, ref erweishuzu_Circle_Array, SelectChanel);

                int temp_Index = erweishuzu_Circle_Array[1].Index_Array_Save;

                Action<bool> action = (x) =>
                {
                    chart1.Series.Clear();
                    for (int j = 0; j < 40; j++)
                    {
                        if (SelectChanel[j+1] == false)
                            continue;
                    
                        serial_ch[j].Points.Clear();
                        temp_Index = erweishuzu_Circle_Array[j + 1].Index_Array_Save;
                        for (int i = 0; i < Display_Num; i++)
                        {
                            serial_ch[j].Points.AddXY(i, (erweishuzu_Circle_Array[j + 1].Circle_Read(ref temp_Index) + j * 1000) * 4.5 / 8388607.0);
                        }

                        chart1.Series.Add(serial_ch[j]);
                    }
                };
                this.Invoke(action, true);
            }
            else//阻抗模式
            {
                //JieXiShuJu(ref recQueue_test, ref erweishuzu_Circle_Array, SelectChanel);

                //Action<double[]> action = (button) =>
                //{
                //    for (int i = 1; i <= 32; i++)
                //    {
                //        Change_ButtonColor(ref Array_Button[i], button[i]);
                //    }
                //    for (int i = 37; i <= 40; i++)
                //    {
                //        Change_ButtonColor(ref Array_Button[i], button[i]);
                //    }
                //};
                
                //for (int i = 1; i < 41; i++)
                //{
                //    //FlagButton_BoolArray[i] = (erweishuzu_Circle_Array[i].Circle_Array_T[erweishuzu_Circle_Array[i].Index_Array_Save] * 4.5 * coef_a / 8388607.0 - coef_b);
                //    FlagButton_BoolArray[i] = (erweishuzu_Circle_Array[i].Circle_Array_T[erweishuzu_Circle_Array[i].Index_Array_Save] * 4.5 / 8388607.0);
                
                //}
                //this.Invoke(action, FlagButton_BoolArray);


                //为最大值和最小值赋初值
                for (int i = 0; i < TongDaoShu - 1; i++)
                {
                    maxValue[i] = 0;
                    minValue[i] = 1000;
                }

                if (test_FIR_ttt == false)
                    //JieXiShuJu(ref recQueue_test, ref erweishuzu_Circle_Array, SelectChanel);
                    JieXiShuJu_tt(ref recQueue_test, ref erweishuzu_Circle_Array, SelectChanel);
                else
                    //JieXiLvBoShuJu(ref recQueue_test, ref FIR_h, ref erweishuzu_Circle_Array, SelectChanel);
                    JieXiLvBoShuJu_tt(ref recQueue_test, ref FIR_h, ref erweishuzu_Circle_Array, SelectChanel);

                int temp_Index = erweishuzu_Circle_Array[1].Index_Array_Save;

                Action<bool> action = (x) =>
                {
                    for (int j = 0; j < 40; j++)
                    {
                        temp_Index = erweishuzu_Circle_Array[j + 1].Index_Array_Save;
                        for (int i = 0; i < 500; i++)//500组数据对应500毫秒
                        {
                            //serial_ch[j].Points.AddXY(i, (erweishuzu_Circle_Array[j + 1].Circle_Read(ref temp_Index) + j * 1000) * 4.5 / 8388607.0);
                            //获取每个通道的最小值
                            maxValue[j] = (maxValue[j] < (erweishuzu_Circle_Array[j + 1].Circle_Read(ref temp_Index)) ? (erweishuzu_Circle_Array[j + 1].Circle_Read(ref temp_Index)) : maxValue[j]);
                            minValue[j] = (minValue[j] > (erweishuzu_Circle_Array[j + 1].Circle_Read(ref temp_Index)) ? (erweishuzu_Circle_Array[j + 1].Circle_Read(ref temp_Index)) : minValue[j]);
                        }
                        youxiaoValue[j + 1] = (maxValue[j] - minValue[j]) * 0.70710678 * 4.5 / 8388607.0;
                    }

                    for (int i = 1; i <= 32; i++)
                    {
                        Change_ButtonColor(ref Array_Button[i], youxiaoValue[i]);
                    }
                    for (int i = 37; i <= 40; i++)
                    {
                        Change_ButtonColor(ref Array_Button[i], youxiaoValue[i]);
                    }
                };
                this.Invoke(action, true);
            }
        }

        void DisplayChart_ThreadingTimer_DingZu(object state)
        {
            Action<bool> action = (x) =>
            {
                DingZuSave_Button.Enabled = true;
            };
            this.Invoke(action, true);
        }

        void Change_ButtonColor(ref Button Param_Button, double ZuKang)
        {
            Param_Button.Text = ZuKang.ToString();
            
            //if (ZuKang > 430)
            //{
            //    Param_Button.BackColor = Color.Fuchsia;//20.0
            //}
            //else if (ZuKang > 400)
            //{
            //    Param_Button.BackColor = Color.Indigo;//18.9
            //}
            //else if (ZuKang > 370)
            //{
            //    Param_Button.BackColor = Color.OrangeRed;//17.9
            //}
            //else if (ZuKang > 340)
            //{
            //    Param_Button.BackColor = Color.DarkOrange;//16.8
            //}
            //else if (ZuKang > 310)
            //{
            //    Param_Button.BackColor = Color.Sienna;//15.7
            //}
            //else if (ZuKang > 280)
            //{
            //    Param_Button.BackColor = Color.Khaki;//14.6
            //}
            //else if (ZuKang > 250)
            //{
            //    Param_Button.BackColor = Color.Yellow;//13.6
            //}
            //else if (ZuKang > 220)
            //{
            //    Param_Button.BackColor = Color.GreenYellow;//12.5
            //}
            //else if (ZuKang > 190)
            //{
            //    Param_Button.BackColor = Color.Green;//11.4
            //}
            //else if (ZuKang > 160)
            //{
            //    Param_Button.BackColor = Color.DarkGreen;//10.4
            //}
            //else if (ZuKang > 130)
            //{
            //    Param_Button.BackColor = Color.SkyBlue;//9.3
            //}
            //else if (ZuKang > 100)
            //{
            //    Param_Button.BackColor = Color.DarkTurquoise;//8.2
            //}
            //else if (ZuKang > 70)
            //{
            //    Param_Button.BackColor = Color.DeepSkyBlue;//7.1
            //}
            //else if (ZuKang > 40)
            //{
            //    Param_Button.BackColor = Color.Blue;//6
            //}
            //else
            //{
            //    Param_Button.BackColor = Color.MidnightBlue;//5
            //}
        }

        private void InitChart()
        {
            //定义图表区域
            this.chart1.ChartAreas.Clear();
            ChartArea chartArea1 = new ChartArea("C1");
            this.chart1.ChartAreas.Add(chartArea1);
            //定义存储和显示点的容器
            this.chart1.Series.Clear();
            Series series1 = new Series("S1");
            series1.ChartArea = "C1";
            this.chart1.Series.Add(series1);
            //设置图表显示样式
            //this.chart1.ChartAreas[0].AxisY.Minimum = 0;
            //this.chart1.ChartAreas[0].AxisY.Maximum = 100;
            this.chart1.ChartAreas[0].AxisX.Interval = 5;
            this.chart1.ChartAreas[0].AxisX.MajorGrid.LineColor = System.Drawing.Color.Silver;
            this.chart1.ChartAreas[0].AxisY.MajorGrid.LineColor = System.Drawing.Color.Silver;
            //设置标题
            this.chart1.Titles.Clear();
            this.chart1.Titles.Add("S01");
            this.chart1.Titles[0].Text = "11235";
            this.chart1.Titles[0].ForeColor = Color.RoyalBlue;
            this.chart1.Titles[0].Font = new System.Drawing.Font("Microsoft Sans Serif", 12F);
            //设置图表显示样式
            this.chart1.Series[0].Color = Color.Red;
            this.chart1.Series[0].ChartType = SeriesChartType.Line;
            this.chart1.Series[0].Points.Clear();

            //chart1.Series.Clear();
            chart1.ChartAreas[0].CursorX.IsUserEnabled = true;
            chart1.ChartAreas[0].CursorX.IsUserSelectionEnabled = true;
            chart1.ChartAreas[0].AxisX.ScaleView.Zoomable = true;

            chart1.ChartAreas[0].AxisX.ScrollBar.IsPositionedInside = true;
            chart1.ChartAreas[0].AxisX.ScrollBar.Size = 10;///////
            chart1.ChartAreas[0].AxisX.ScrollBar.ButtonStyle = ScrollBarButtonStyles.All;

            chart1.ChartAreas[0].AxisX.ScaleView.SmallScrollSize = double.NaN;
            chart1.ChartAreas[0].AxisX.ScaleView.SmallScrollSize = 2;

            chart1.ChartAreas[0].AxisY.Maximum = System.Double.NaN;
            chart1.ChartAreas[0].AxisY.Minimum = System.Double.NaN;

            chart1.ChartAreas[0].CursorY.IsUserEnabled = true;
            chart1.ChartAreas[0].CursorY.IsUserSelectionEnabled = true;
            chart1.ChartAreas[0].AxisY.ScaleView.Zoomable = true;
            chart1.ChartAreas[0].CursorY.Interval = 0.00001;//控制chart控件的放大精度

            chart1.ChartAreas[0].AxisY.ScrollBar.IsPositionedInside = true;
            chart1.ChartAreas[0].AxisY.ScrollBar.Size = 10;///////
            chart1.ChartAreas[0].AxisY.ScrollBar.ButtonStyle = ScrollBarButtonStyles.All;

            chart1.ChartAreas[0].AxisY.ScaleView.SmallScrollSize = double.NaN;
            chart1.ChartAreas[0].AxisY.ScaleView.SmallScrollSize = 2;
        }





        /*
         以下是各种按键的回调函数
         */
        private void start_button_Click(object sender, EventArgs e)
        {
            if (StartConnectOrNot == true)
            {
                StartConnectOrNot = false;
                start_button.Text = "停止连接";
                int len_Xiu = 2;
                byte[] temp_Byte = new byte[2];
                temp_Byte[0] = Convert.ToByte('S');
                temp_Byte[1] = Convert.ToByte('S');
                //发送两次，防止指令丢失
                if (MyDevice.BulkOutEndPt.XferData(ref temp_Byte, ref len_Xiu) != true)
                    MessageBox.Show("error", "fail to begin");
                else
                    recThread_Flag = true;
                if (MyDevice.BulkOutEndPt.XferData(ref temp_Byte, ref len_Xiu) != true)
                    MessageBox.Show("error", "fail to begin");
                else
                    recThread_Flag = true;

                Flag__DisplayChart_ThreadingTimer__ReturnChart_Button_Click = true;

                ClearFangDa_Button_Click(this, null);//保证每次上位机开启时下位机的放大倍数为1

                //链接设备两秒后可以使用“定组保存”
                //Timer_ThreadingTimer_DingZu = new System.Threading.Timer(new TimerCallback(DisplayChart_ThreadingTimer_DingZu), null, 2000, 0);
            }
            else
            {
                StartConnectOrNot = true;
                start_button.Text = "连接设备";
                int len_Xiu = 2;
                byte[] temp_Byte = new byte[2];
                temp_Byte[0] = Convert.ToByte('E');
                temp_Byte[1] = Convert.ToByte('E');
                //发送两次，防止指令丢失
                if (MyDevice.BulkOutEndPt.XferData(ref temp_Byte, ref len_Xiu) != true)
                    MessageBox.Show("error", "fail to stop");
                else
                    recThread_Flag = false;
                if (MyDevice.BulkOutEndPt.XferData(ref temp_Byte, ref len_Xiu) != true)
                    MessageBox.Show("error", "fail to stop");
                else
                    recThread_Flag = false;

                Flag__DisplayChart_ThreadingTimer__ReturnChart_Button_Click = false;

                //断开连接时，立刻失能“定组保存”
                //DingZuSave_Button.Enabled = false;
            }
        }

        private void saveBegin_Button_Click(object sender, EventArgs e)
        {
            if (SaveFileBeginOrNot == true)
            {
                SaveFileBeginOrNot = false;

                recQueue_Save = new ConcurrentQueue<byte[]>();//清除上一次的数据  PS：如果上一次将数据保存成txt文本形式，就不用加这句话，但是有的时候一不小心收集到错误数据，所以会产生“虽然recQueue_Save保存了一些数据，但是使用者并没有将其导出”的情况
                saveBegin_Button.Text = "停止保存";

                Flag_recQueue_Save = true;
            }
            else
            {
                SaveFileBeginOrNot = true;
                saveBegin_Button.Text = "开始保存";

                Flag_recQueue_Save = false;
            }
        }

        private void save_button_Click(object sender, EventArgs e)
        {
            save_shuju();
        }

        void save_shuju()
        {
            #region
            System.Windows.Forms.SaveFileDialog sfd = new SaveFileDialog();//注意 这里是SaveFileDialog,不是OpenFileDialog
            sfd.DefaultExt = "txt";
            sfd.Filter = "文本文件(*.txt)|*.txt";
            if (sfd.ShowDialog() == DialogResult.OK)
            {

                StringBuilder recBuffer16 = new StringBuilder();//定义16进制接收缓存


                List<byte> temp_listbyte = new List<byte>();
                int shuliang_recQueue_Save = recQueue_Save.Count;
                byte[] temp_Buf = new byte[len];
                for (int i = 0; i < shuliang_recQueue_Save; i++)
                {
                    //temp_listbyte.AddRange((byte[])recQueue_Save.Dequeue());
                    recQueue_Save.TryDequeue(out temp_Buf);
                    temp_listbyte.AddRange(temp_Buf);
                }

                List<Int32> jishu_rem_SaveFile = new List<Int32>();
                int last_one = 0;
                int current_one = 0;
                for (int i = 0; i < (temp_listbyte.Count - 1); i++)
                {
                    if ((temp_listbyte[i] == 0x55) && (temp_listbyte[i + 1] == 0x55) && (temp_listbyte[i + 2] == 0xaa) && (temp_listbyte[i + 3] == 0xaa))
                    {
                        current_one = temp_listbyte[i + 4] * 65536 + temp_listbyte[i + 5] * 256 + temp_listbyte[i + 6];
                        if ((current_one - last_one) != 1)
                        {
                            jishu_rem_SaveFile.Add(i);//把有差距的位置记录下来
                        }
                        last_one = current_one;
                        i += 120;//每组128，保险起见120
                    }
                }
                jishu_rem_SaveFile.Add((temp_listbyte.Count - 128 + jishu_rem_SaveFile[0]));

                if (jishu_rem_SaveFile.Count != 2)//如果有断层，直接停止保存
                {
                    int[] duanceng = new int[2];
                    duanceng[0] = temp_listbyte[jishu_rem_SaveFile[1] + 4]*65536 + temp_listbyte[jishu_rem_SaveFile[1] + 5]*256 + temp_listbyte[jishu_rem_SaveFile[1] + 6];
                    duanceng[1] = temp_listbyte[jishu_rem_SaveFile[2] + 4] * 65536 + temp_listbyte[jishu_rem_SaveFile[2] + 5] * 256 + temp_listbyte[jishu_rem_SaveFile[2] + 6];
                    MessageBox.Show(duanceng[0].ToString() + " " + duanceng[1].ToString(), "断层");
                    return;
                }

                //for (; jishu_rem_SaveFile.Count >= 2; )
                //{
                //    StringBuilder temp_StringBuilder = new StringBuilder();
                //    for (int j = jishu_rem_SaveFile[0]; j < jishu_rem_SaveFile[1]; j++)
                //    {
                //        temp_StringBuilder.AppendFormat("{0:X2}" + " ", temp_listbyte[j]);
                //    }

                //    string[] temp_Str = sfd.FileName.Split('.');
                //    int begin_connect_listbyte = temp_listbyte[jishu_rem_SaveFile[0] + 4] * 65536 + temp_listbyte[jishu_rem_SaveFile[0] + 5] * 256 + temp_listbyte[jishu_rem_SaveFile[0] + 6];
                //    int end_connect_listbyte = temp_listbyte[jishu_rem_SaveFile[1] + 4 - 128] * 65536 + temp_listbyte[jishu_rem_SaveFile[1] + 5 - 128] * 256 + temp_listbyte[jishu_rem_SaveFile[1] + 6 - 128];
                //    jishu_rem_SaveFile.RemoveAt(0);
                //    string fileName_tt = temp_Str[0] + "_" + begin_connect_listbyte.ToString() + "_" + end_connect_listbyte.ToString() + ".txt";//std.FileName表示对话框中的路径名称
                //    FileStream fs_tt = null;
                //    try
                //    {
                //        File.Delete(fileName_tt);//有重名的就删除掉
                //        fs_tt = new FileStream(fileName_tt, FileMode.OpenOrCreate);//返回文件表示符

                //        using (StreamWriter writer = new StreamWriter(fs_tt))
                //        {
                //            writer.Write(temp_StringBuilder);
                //        }
                //    }
                //    finally
                //    {
                //        if (fs_tt != null)
                //            fs_tt.Dispose();
                //    }
                //}


                StringBuilder temp_StringBuilder = new StringBuilder();
                //for (int j = jishu_rem_SaveFile[0]; j < jishu_rem_SaveFile[1]; j++)
                for (int j = jishu_rem_SaveFile[0]; j < jishu_rem_SaveFile[1] + 128; j++)//因为之前会出现断层，按照以前的算法会莫明其妙的少载入一组，现在把这组填回来。PS:但凡能到达这里就说明没有断层
                {
                    temp_StringBuilder.AppendFormat("{0:X2}" + " ", temp_listbyte[j]);
                }

                string[] temp_Str = sfd.FileName.Split('.');
                string fileName_tt = sfd.FileName;//std.FileName表示对话框中的路径名称
                FileStream fs_tt = null;
                try
                {
                    File.Delete(fileName_tt);//有重名的就删除掉
                    fs_tt = new FileStream(fileName_tt, FileMode.OpenOrCreate);//返回文件表示符

                    using (StreamWriter writer = new StreamWriter(fs_tt))
                    {
                        writer.Write(temp_StringBuilder);
                    }
                }
                finally
                {
                    if (fs_tt != null)
                        fs_tt.Dispose();
                }
            }
            #endregion
        }

        private void ReturnChart_Button_Click(object sender, EventArgs e)
        {
            Flag__DisplayChart_ThreadingTimer__ReturnChart_Button_Click = false;//刷新过程中禁用chart定时器刷新

            InitChart();

            Flag__DisplayChart_ThreadingTimer__ReturnChart_Button_Click = true;
        }

        private void LoadXiShu_Button_Click(object sender, EventArgs e)
        {
            //chart1.Series.Remove(serial_ch1);

            System.Windows.Forms.OpenFileDialog sfd = new OpenFileDialog();//注意 这里是OpenFileDialog,不是SaveFileDialog
            sfd.DefaultExt = "txt";
            sfd.Filter = "文本文件(*.txt)|*.txt";
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                //DoExport(this.listView1, sfd.FileName);
                StreamReader rd = File.OpenText(sfd.FileName);
                string s = rd.ReadLine();
                string[] ss = s.Split('\t');
                //byte[] bytearray = new byte[ss.Length];
                double temp_byte = 0;
                //connect_listbyte_ttt.Clear();
                for (int i = 0; i < ss.Length; i++)
                {
                    temp_byte = ChangeDataToD(ss[i]);
                    FIR_h[i] = temp_byte;
                    //connect_listbyte_ttt.Add(temp_byte);
                }
            }
        }
        /*
         * 将科学计数法转换成单精度小数
         */
        private double ChangeDataToD(string strData)
        {
            double dData = 0.0;
            bool Zhengshu = true;
            bool Mi_Zhengshu = true;

            int Index_Point = strData.IndexOf('.');//找到小数点
            int Index_E = strData.IndexOf('e');//如果存在e，找到e，否则返回-1

            //double ii = Math.Pow(10, 2);
            /******************************************
             * 进行三次判断：有没有E，小数的正负，幂次的正负
             * 
             * 
             * **********************************************************/

            if (Index_E != -1)//如果数据里面有E
            {
                if (strData[0] == '-')//负数解析,eg:1.23e-4
                {
                    Zhengshu = false;
                    double temp_E_Double = 0;
                    for (int i = 1; i < Index_Point; i++)//整数部分,eg:1
                    {
                        //var test = (Math.Pow(10, (Index_Point - i - 1)));
                        //var test2 = Convert.ToInt16(strData[i]) - 48;
                        dData += (Convert.ToInt16(strData[i]) - 48) * (Math.Pow(10, (Index_Point - i - 1)));//这句话整数负数不一样
                    }

                    for (int i = Index_Point + 1; i < Index_E; i++)//小数部分,eg:0.23
                    {
                        dData += (Convert.ToInt16(strData[i]) - 48) * (Math.Pow(10, (Index_Point - i)));
                    }

                    //判断幂,eg:-4
                    if (strData[Index_E + 1] == '-')
                    {
                        Mi_Zhengshu = false;
                        for (int i = (Index_E + 2); i < strData.Length; i++)
                        {
                            temp_E_Double = (Convert.ToInt16(strData[i]) - 48) * (Math.Pow(10, (strData.Length - 1 - i)));
                        }
                        temp_E_Double = -temp_E_Double;
                        dData = -dData * Math.Pow(10, temp_E_Double);
                    }
                    else
                    {
                        Mi_Zhengshu = true;
                        for (int i = (Index_E + 1); i < strData.Length; i++)//这里的加1还是加2需要注意一下
                        {
                            temp_E_Double = (Convert.ToInt16(strData[i]) - 48) * (Math.Pow(10, (strData.Length - 1 - i)));
                        }
                        dData = -dData * Math.Pow(10, temp_E_Double);
                    }

                }
                else//正数解析
                {
                    Zhengshu = true;
                    double temp_E_Double = 0;
                    for (int i = 0; i < Index_Point; i++)//整数部分,eg:1
                    {
                        //var test = (Math.Pow(10, (Index_Point - i - 1)));
                        //var test2 = Convert.ToInt16(strData[i]) - 48;
                        dData += (Convert.ToInt16(strData[i]) - 48) * (Math.Pow(10, (Index_Point - i - 1)));
                    }

                    for (int i = Index_Point + 1; i < Index_E; i++)//小数部分,eg:0.23
                    {
                        dData += (Convert.ToInt16(strData[i]) - 48) * (Math.Pow(10, (Index_Point - i)));
                    }

                    //判断幂,eg:-4
                    if (strData[Index_E + 1] == '-')
                    {
                        Mi_Zhengshu = false;
                        for (int i = (Index_E + 2); i < strData.Length; i++)
                        {
                            temp_E_Double = (Convert.ToInt16(strData[i]) - 48) * (Math.Pow(10, (strData.Length - 1 - i)));
                        }
                        temp_E_Double = -temp_E_Double;
                        dData = dData * Math.Pow(10, temp_E_Double);
                    }
                    else
                    {
                        Mi_Zhengshu = true;
                        for (int i = (Index_E + 1); i < strData.Length; i++)//这里的加1还是加2需要注意一下
                        {
                            temp_E_Double = (Convert.ToInt16(strData[i]) - 48) * (Math.Pow(10, (strData.Length - 1 - i)));
                        }
                        dData = dData * Math.Pow(10, temp_E_Double);
                    }
                }
            }
            else//如果数据里面没有E
            {
                if (strData[0] == '-')//负数解析,eg:1.23e-4
                {
                    Zhengshu = false;
                    //double temp_E_Double = 0;
                    for (int i = 1; i < Index_Point; i++)//整数部分,eg:1
                    {
                        //var test = (Math.Pow(10, (Index_Point - i - 1)));
                        //var test2 = Convert.ToInt16(strData[i]) - 48;
                        dData += (Convert.ToInt16(strData[i]) - 48) * (Math.Pow(10, (Index_Point - i - 1)));//这句话整数负数不一样
                    }

                    for (int i = Index_Point + 1; i < strData.Length; i++)//小数部分,eg:0.23
                    {
                        dData += (Convert.ToInt16(strData[i]) - 48) * (Math.Pow(10, (Index_Point - i)));
                    }
                    dData = -dData;
                }
                else//正数解析
                {
                    Zhengshu = true;
                    //double temp_E_Double = 0;
                    for (int i = 0; i < Index_Point; i++)//整数部分,eg:1
                    {
                        //var test = (Math.Pow(10, (Index_Point - i - 1)));
                        //var test2 = Convert.ToInt16(strData[i]) - 48;
                        dData += (Convert.ToInt16(strData[i]) - 48) * (Math.Pow(10, (Index_Point - i - 1)));
                    }

                    for (int i = Index_Point + 1; i < strData.Length; i++)//小数部分,eg:0.23
                    {
                        dData += (Convert.ToInt16(strData[i]) - 48) * (Math.Pow(10, (Index_Point - i)));
                    }
                }
            }

            return dData;
        }

        private void sleep_Button_Click(object sender, EventArgs e)
        {
            //Thread.Sleep(10000);
            if(test_FIR_ttt == false)
            {
                test_FIR_ttt = true;
                sleep_Button.Text = "停止滤波";
            }
            else
            {
                test_FIR_ttt = false;
                sleep_Button.Text = "开始滤波";
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void Chanel_ListView_MouseClick(object sender, MouseEventArgs e)
        {
            int index_List = this.Chanel_ListView.SelectedItems[0].Index;
            if (lvi_Array[index_List].SubItems[2].Text == " ")
            {
                lvi_Array[index_List].SubItems[2].Text = "running";
                SelectChanel[index_List + 1] = true;
            }
            else
            {
                lvi_Array[index_List].SubItems[2].Text = " ";
                SelectChanel[index_List + 1] = false;
            }
        }

        private void ModeChange_Button_Click(object sender, EventArgs e)
        {
            int len_Xiu = 6;
            byte[] buf_Xiu = new byte[len_Xiu];
            buf_Xiu[0] = Convert.ToByte('T');
            buf_Xiu[1] = Convert.ToByte('S');
            //buf_Xiu[2] = Convert.ToByte((string)XiuGaiBeiShu_ComboBox.Text);
            buf_Xiu[3] = Convert.ToByte('M');
            buf_Xiu[4] = Convert.ToByte('D');
            buf_Xiu[5] = Convert.ToByte('E');

            switch (ModeChange_ComboBox.Text)
            {
                case "0":
                    buf_Xiu[2] = Convert.ToByte('0');
                    ChangeModeFlag_Int = 0;
                    My_TabControl.SelectTab(0);
                    Timer_ThreadingTimer.Change(0, 100);//更改刷新时间，100毫秒一刷新
                    break;//输入模式
                case "1":
                    buf_Xiu[2] = Convert.ToByte('1');
                    ChangeModeFlag_Int = 1;
                    My_TabControl.SelectTab(1);
                    Timer_ThreadingTimer.Change(0, 500);//更改刷新时间，500毫秒一刷新
                    break;//阻抗检测
            }

            if (MyDevice.BulkOutEndPt.XferData(ref buf_Xiu, ref len_Xiu) != true)
                MessageBox.Show("error", "模式切换失败");
            if (MyDevice.BulkOutEndPt.XferData(ref buf_Xiu, ref len_Xiu) != true)
                MessageBox.Show("error", "模式切换失败");
        }

        private void XiuGaiBeiShu_Button_Click(object sender, EventArgs e)
        {
            int len_Xiu = 6;
            byte[] buf_Xiu = new byte[len_Xiu];
            byte[] temp_Byte = new byte[8];
            buf_Xiu[0] = Convert.ToByte('T');
            buf_Xiu[1] = Convert.ToByte('S');
            //buf_Xiu[2] = Convert.ToByte((string)XiuGaiBeiShu_ComboBox.Text);
            buf_Xiu[3] = Convert.ToByte('G');
            buf_Xiu[4] = Convert.ToByte('D');
            buf_Xiu[5] = Convert.ToByte('E');

            //switch (XiuGaiBeiShu_ComboBox.Text)
            //{
            //    case "1": buf_Xiu[2] = Convert.ToByte('0'); break;
            //    case "2": buf_Xiu[2] = Convert.ToByte('1'); break;
            //    case "4": buf_Xiu[2] = Convert.ToByte('2'); break;
            //    case "6": buf_Xiu[2] = Convert.ToByte('3'); break;
            //    case "8": buf_Xiu[2] = Convert.ToByte('4'); break;
            //    case "12": buf_Xiu[2] = Convert.ToByte('5'); break;
            //    case "24": buf_Xiu[2] = Convert.ToByte('6'); break;
            //}
            //for(int i = 0; i < 8; i++)
            //{
            //    //temp_Byte[i] = Convert.ToByte(comboBox_Array[i].Text);
            //    switch (comboBox_Array[i].Text)
            //    {
            //        case "1": temp_Byte[i] = 0; break;
            //        case "2": temp_Byte[i] = 1; break;
            //        case "4": temp_Byte[i] = 2; break;
            //        case "6": temp_Byte[i] = 3; break;
            //        case "8": temp_Byte[i] = 4; break;
            //        case "12": temp_Byte[i] = 5; break;
            //        case "24": temp_Byte[i] = 6; break;
            //    }
            //}
            //buf_Xiu[2] = (byte)(temp_Byte[0] * 32 + temp_Byte[1] * 4 + temp_Byte[2] / 2);
            //buf_Xiu[5] = (byte)((temp_Byte[2] % 2) * 128 + temp_Byte[3] * 16 + temp_Byte[4]*2 + temp_Byte[5] / 4);
            //buf_Xiu[4] = (byte)((temp_Byte[5] % 4) * 64 + temp_Byte[6] * 8 + temp_Byte[7]);

            //if (MyDevice.BulkOutEndPt.XferData(ref buf_Xiu, ref len_Xiu) != true)
            //    MessageBox.Show("error", "修改放大倍数失败");
            //if (MyDevice.BulkOutEndPt.XferData(ref buf_Xiu, ref len_Xiu) != true)
            //    MessageBox.Show("error", "修改放大倍数失败");

            
            //注意“XiuGaiBeiShu1_ComboBox”和“XiuGaiBeiShu_ComboBox”不是一个玩意
            int temp_DiJiLu = Convert.ToInt16(XiuGaiBeiShu1_ComboBox.Text);
            int flag_Test = 0;
            if (temp_DiJiLu <= 8)
            {
                buf_Xiu[3] = Convert.ToByte('G');
                flag_Test = 0;
            }
            else if(temp_DiJiLu <= 16)
            {
                buf_Xiu[3] = Convert.ToByte('H');
                flag_Test = 1;
            }
            else if(temp_DiJiLu <= 24)
            {
                buf_Xiu[3] = Convert.ToByte('I');
                flag_Test = 2;
            }
            else if(temp_DiJiLu <= 32)
            {
                buf_Xiu[3] = Convert.ToByte('J');
                flag_Test = 3;
            }
            else
            {
                buf_Xiu[3] = Convert.ToByte('K');
                flag_Test = 4;
            }

            int temp_8 = (temp_DiJiLu - 1) % 8;
            int temp_88 = Convert.ToInt16(XiuGaiBeiShu_ComboBox.Text);

            lvi_Array[(temp_DiJiLu - 1)].SubItems[3].Text = temp_88.ToString();
            //buf_Xiu[2] = (byte)(temp_88 / 65536);
            //buf_Xiu[5] = (byte)(temp_88 % 65536 / 256);
            //buf_Xiu[4] = (byte)(temp_88 % 256);

            switch(temp_88)
            {
                case 1: temp_88 = 0; break;
                case 2: temp_88 = 1; break;
                case 4: temp_88 = 2; break;
                case 6: temp_88 = 3; break;
                case 8: temp_88 = 4; break;
                case 12: temp_88 = 5; break;
                case 24: temp_88 = 6; break;
            }

            if(temp_8 == 7)
            {
                FangDa_FenBie[flag_Test][2] &= 248;
                FangDa_FenBie[flag_Test][2] |= (byte)(temp_88);
            }
            else if(temp_8 == 6)
            {
                FangDa_FenBie[flag_Test][2] &= 199;
                FangDa_FenBie[flag_Test][2] |= (byte)(temp_88 << 3);
            }
            else if(temp_8 == 5)
            {
                FangDa_FenBie[flag_Test][2] &= 63;
                FangDa_FenBie[flag_Test][1] &= 254;
                FangDa_FenBie[flag_Test][2] |= (byte)((temp_88 % 4) << 6);
                FangDa_FenBie[flag_Test][1] |= (byte)((temp_88 / 4));
            }
            else if(temp_8 == 4)
            {
                FangDa_FenBie[flag_Test][1] &= 241;
                FangDa_FenBie[flag_Test][1] |= (byte)(temp_88 << 1);
            }
            else if(temp_8 == 3)
            {
                FangDa_FenBie[flag_Test][1] &= 143;
                FangDa_FenBie[flag_Test][1] |= (byte)(temp_88 << 4);
            }
            else if(temp_8 == 2)
            {
                FangDa_FenBie[flag_Test][1] &= 127;
                FangDa_FenBie[flag_Test][0] &= 252;
                FangDa_FenBie[flag_Test][1] |= (byte)((temp_88 % 2) << 7);
                FangDa_FenBie[flag_Test][0] |= (byte)((temp_88 / 2));
            }
            else if(temp_8 == 1)
            {
                FangDa_FenBie[flag_Test][0] &= 227;
                FangDa_FenBie[flag_Test][0] |= (byte)(temp_88 << 2);
            }
            else if(temp_8 == 0)
            {
                FangDa_FenBie[flag_Test][0] &= 31;
                FangDa_FenBie[flag_Test][0] |= (byte)(temp_88 << 5);
            }

            buf_Xiu[2] = FangDa_FenBie[flag_Test][0];
            buf_Xiu[5] = FangDa_FenBie[flag_Test][1];
            buf_Xiu[4] = FangDa_FenBie[flag_Test][2];

            //System.Diagnostics.Debug.WriteLine("XiuGai {0} {1} {2}", buf_Xiu[2], buf_Xiu[5], buf_Xiu[4]);
            if (MyDevice.BulkOutEndPt.XferData(ref buf_Xiu, ref len_Xiu) != true)
                MessageBox.Show("error", "修改放大倍数失败");

            Thread.Sleep(100);

            if (MyDevice.BulkOutEndPt.XferData(ref buf_Xiu, ref len_Xiu) != true)
                MessageBox.Show("error", "修改放大倍数失败");
        }

        private void TongShiFangDa_Button_Click(object sender, EventArgs e)
        {
            TongShiFangDa_tt("8");
            TongShiFangDa_tt("16");
            TongShiFangDa_tt("24");
            TongShiFangDa_tt("32");
            TongShiFangDa_tt("40");
        }

        void TongShiFangDa_tt(string xiu)
        {
            int len_Xiu = 6;
            byte[] buf_Xiu = new byte[len_Xiu];
            byte[] temp_Byte = new byte[8];
            buf_Xiu[0] = Convert.ToByte('T');
            buf_Xiu[1] = Convert.ToByte('S');
            //buf_Xiu[2] = Convert.ToByte((string)XiuGaiBeiShu_ComboBox.Text);
            buf_Xiu[3] = Convert.ToByte('G');
            buf_Xiu[4] = Convert.ToByte('D');
            buf_Xiu[5] = Convert.ToByte('E');

            //注意“XiuGaiBeiShu1_ComboBox”和“XiuGaiBeiShu_ComboBox”不是一个玩意
            int temp_DiJiLu = Convert.ToInt16(xiu);
            int flag_Test = 0;
            if (temp_DiJiLu <= 8)
            {
                buf_Xiu[3] = Convert.ToByte('G');
                flag_Test = 0;
            }
            else if (temp_DiJiLu <= 16)
            {
                buf_Xiu[3] = Convert.ToByte('H');
                flag_Test = 1;
            }
            else if (temp_DiJiLu <= 24)
            {
                buf_Xiu[3] = Convert.ToByte('I');
                flag_Test = 2;
            }
            else if (temp_DiJiLu <= 32)
            {
                buf_Xiu[3] = Convert.ToByte('J');
                flag_Test = 3;
            }
            else
            {
                buf_Xiu[3] = Convert.ToByte('K');
                flag_Test = 4;
            }

            int temp_8 = (temp_DiJiLu - 1) % 8;
            int temp_88 = Convert.ToInt16(XiuGaiBeiShu_ComboBox.Text);

            lvi_Array[(temp_DiJiLu - 1)].SubItems[3].Text = temp_88.ToString();
            //buf_Xiu[2] = (byte)(temp_88 / 65536);
            //buf_Xiu[5] = (byte)(temp_88 % 65536 / 256);
            //buf_Xiu[4] = (byte)(temp_88 % 256);

            switch (temp_88)
            {
                case 1: temp_88 = 0; break;
                case 2: temp_88 = 1; break;
                case 4: temp_88 = 2; break;
                case 6: temp_88 = 3; break;
                case 8: temp_88 = 4; break;
                case 12: temp_88 = 5; break;
                case 24: temp_88 = 6; break;
            }

            //if (temp_8 == 7)
            //{
            //    FangDa_FenBie[flag_Test][2] &= 248;
            //    FangDa_FenBie[flag_Test][2] |= (byte)(temp_88);
            //}
            //else if (temp_8 == 6)
            //{
            //    FangDa_FenBie[flag_Test][2] &= 199;
            //    FangDa_FenBie[flag_Test][2] |= (byte)(temp_88 << 3);
            //}
            //else if (temp_8 == 5)
            //{
            //    FangDa_FenBie[flag_Test][2] &= 63;
            //    FangDa_FenBie[flag_Test][1] &= 254;
            //    FangDa_FenBie[flag_Test][2] |= (byte)((temp_88 % 4) << 6);
            //    FangDa_FenBie[flag_Test][1] |= (byte)((temp_88 / 4));
            //}
            //else if (temp_8 == 4)
            //{
            //    FangDa_FenBie[flag_Test][1] &= 241;
            //    FangDa_FenBie[flag_Test][1] |= (byte)(temp_88 << 1);
            //}
            //else if (temp_8 == 3)
            //{
            //    FangDa_FenBie[flag_Test][1] &= 143;
            //    FangDa_FenBie[flag_Test][1] |= (byte)(temp_88 << 4);
            //}
            //else if (temp_8 == 2)
            //{
            //    FangDa_FenBie[flag_Test][1] &= 127;
            //    FangDa_FenBie[flag_Test][0] &= 252;
            //    FangDa_FenBie[flag_Test][1] |= (byte)((temp_88 % 2) << 7);
            //    FangDa_FenBie[flag_Test][0] |= (byte)((temp_88 / 2));
            //}
            //else if (temp_8 == 1)
            //{
            //    FangDa_FenBie[flag_Test][0] &= 227;
            //    FangDa_FenBie[flag_Test][0] |= (byte)(temp_88 << 2);
            //}
            //else if (temp_8 == 0)
            //{
            //    FangDa_FenBie[flag_Test][0] &= 31;
            //    FangDa_FenBie[flag_Test][0] |= (byte)(temp_88 << 5);
            //}

            FangDa_FenBie[flag_Test][2] &= 248;
            FangDa_FenBie[flag_Test][2] |= (byte)(temp_88);

            FangDa_FenBie[flag_Test][2] &= 199;
            FangDa_FenBie[flag_Test][2] |= (byte)(temp_88 << 3);

            FangDa_FenBie[flag_Test][2] &= 63;
            FangDa_FenBie[flag_Test][1] &= 254;
            FangDa_FenBie[flag_Test][2] |= (byte)((temp_88 % 4) << 6);
            FangDa_FenBie[flag_Test][1] |= (byte)((temp_88 / 4));

            FangDa_FenBie[flag_Test][1] &= 241;
            FangDa_FenBie[flag_Test][1] |= (byte)(temp_88 << 1);

            FangDa_FenBie[flag_Test][1] &= 143;
            FangDa_FenBie[flag_Test][1] |= (byte)(temp_88 << 4);

            FangDa_FenBie[flag_Test][1] &= 127;
            FangDa_FenBie[flag_Test][0] &= 252;
            FangDa_FenBie[flag_Test][1] |= (byte)((temp_88 % 2) << 7);
            FangDa_FenBie[flag_Test][0] |= (byte)((temp_88 / 2));

            FangDa_FenBie[flag_Test][0] &= 227;
            FangDa_FenBie[flag_Test][0] |= (byte)(temp_88 << 2);

            FangDa_FenBie[flag_Test][0] &= 31;
            FangDa_FenBie[flag_Test][0] |= (byte)(temp_88 << 5);

            buf_Xiu[2] = FangDa_FenBie[flag_Test][0];
            buf_Xiu[5] = FangDa_FenBie[flag_Test][1];
            buf_Xiu[4] = FangDa_FenBie[flag_Test][2];

            System.Diagnostics.Debug.WriteLine("XiuGai {0} {1} {2}", buf_Xiu[2], buf_Xiu[5], buf_Xiu[4]);
            if (MyDevice.BulkOutEndPt.XferData(ref buf_Xiu, ref len_Xiu) != true)
                MessageBox.Show("error", "修改放大倍数失败");

            //Thread.Sleep(100);

            if (MyDevice.BulkOutEndPt.XferData(ref buf_Xiu, ref len_Xiu) != true)
                MessageBox.Show("error", "修改放大倍数失败");
        }

        private void XiuGaiCaiYang_Button_Click(object sender, EventArgs e)
        {
            int len_Xiu = 6;
            byte[] buf_Xiu = new byte[len_Xiu];
            buf_Xiu[0] = Convert.ToByte('T');
            buf_Xiu[1] = Convert.ToByte('S');
            //buf_Xiu[2] = Convert.ToByte((string)XiuGaiBeiShu_ComboBox.Text);
            buf_Xiu[3] = Convert.ToByte('R');///////////////////////////////////////////
            buf_Xiu[4] = Convert.ToByte('D');
            buf_Xiu[5] = Convert.ToByte('E');

            switch (XiuGaiCaiYang_ComboBox.Text)/////////////////////////////////
            {
                case "1": buf_Xiu[2] = Convert.ToByte('1'); break;//
                case "2": buf_Xiu[2] = Convert.ToByte('2'); break;//
                case "3": buf_Xiu[2] = Convert.ToByte('3'); break;//
                case "4": buf_Xiu[2] = Convert.ToByte('4'); break;//
                case "5": buf_Xiu[2] = Convert.ToByte('5'); break;//
                case "6": buf_Xiu[2] = Convert.ToByte('6'); break;//
            }

            if (MyDevice.BulkOutEndPt.XferData(ref buf_Xiu, ref len_Xiu) != true)
                MessageBox.Show("error", "修改采样率失败");
            if (MyDevice.BulkOutEndPt.XferData(ref buf_Xiu, ref len_Xiu) != true)
                MessageBox.Show("error", "修改采样率失败");
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            //ChangColorNum++;
            //if (ChangColorNum >= 360)
            //    ChangColorNum = 0;
            //double temp = Math.Sin(Math.PI * (ChangColorNum / 180.0)) * 127.0 + 127.0;
            //Flash_PictureBox.BackColor = System.Drawing.Color.FromArgb((int)temp, (int)temp, (int)temp);

            //if(DingShiSave_Button.Text == "定时保存")
            //{
            //    DingShiSave_Button.Text = "正在计数";
            //}
            //else
            //{
            //    DingShiSave_Button.Text = "定时保存";
            //    System.Diagnostics.Debug.WriteLine("timer over");
            //    timer1.Stop();
            //}
            saveBegin_Button_Click(this, null);//第二次调用停止保存
            //System.Diagnostics.Debug.WriteLine("timer over");
            timer1.Stop();
            DingShiSave_Button.Text = "定时保存";
            save_button_Click(this, null);
        }

        private void DingShiSave_Button_Click(object sender, EventArgs e)
        {
            //int temp_DingShiSave = Convert.ToInt16(DingShiSave_ComboBox.Text);
            //saveBegin_Button_Click(this, null);
            //for(int i = 0; i < temp_DingShiSave; i++)
            //{
            //    DingShiSave_Button.Text = i.ToString();//将当前时间显示在按键上
            //    DingShiSave_StopWatch.Restart();
            //    while (DingShiSave_StopWatch.ElapsedMilliseconds <= 1000)//直到暂停时间到达
            //    { }
            //}
            timer1.Interval = Convert.ToInt16(DingShiSave_ComboBox.Text) * 1000;
            timer1.Start();
            DingShiSave_Button.Text = "正在计数";
            saveBegin_Button_Click(this, null);//第一次调用时保存
        }

        private void ClearFangDa_Button_Click(object sender, EventArgs e)
        {
            PianXuanFangDa(1, 1);
            PianXuanFangDa(2, 1);
            PianXuanFangDa(3, 1);
            PianXuanFangDa(4, 1);
            PianXuanFangDa(5, 1);

            for(int i = 0; i < 40; i++)
            {
                lvi_Array[i].SubItems[3].Text = "1";
            }
        }

        public void PianXuanFangDa(int PianNum, int BeiShu)
        {
            int len_Xiu = 6;
            byte[] buf_Xiu = new byte[len_Xiu];
            byte[] temp_Byte = new byte[8];
            buf_Xiu[0] = Convert.ToByte('T');
            buf_Xiu[1] = Convert.ToByte('S');
            buf_Xiu[3] = Convert.ToByte('G');
            buf_Xiu[4] = Convert.ToByte('D');
            buf_Xiu[5] = Convert.ToByte('E');

            switch(PianNum)
            {
                case 1: buf_Xiu[3] = Convert.ToByte('G'); break;
                case 2: buf_Xiu[3] = Convert.ToByte('H'); break;
                case 3: buf_Xiu[3] = Convert.ToByte('I'); break;
                case 4: buf_Xiu[3] = Convert.ToByte('J'); break;
                case 5: buf_Xiu[3] = Convert.ToByte('K'); break;
            }

            switch(BeiShu)
            {
                case 1: BeiShu = 0; break;
                case 2: BeiShu = 1; break;
                case 4: BeiShu = 2; break;
                case 6: BeiShu = 3; break;
                case 8: BeiShu = 4; break;
                case 12: BeiShu = 5; break;
                case 24: BeiShu = 6; break;
            }

            buf_Xiu[2] = (byte)(BeiShu * 32 + BeiShu * 4 + BeiShu / 2);
            buf_Xiu[5] = (byte)((BeiShu % 2) * 128 + BeiShu * 16 + BeiShu * 2 + (BeiShu / 4));
            buf_Xiu[4] = (byte)((BeiShu % 4) * 64 + BeiShu * 8 + BeiShu);

            //System.Diagnostics.Debug.WriteLine("{0} {1} {2}", buf_Xiu[2], buf_Xiu[4], buf_Xiu[4]);

            if (MyDevice.BulkOutEndPt.XferData(ref buf_Xiu, ref len_Xiu) != true)
                MessageBox.Show("error", "修改采样率失败");

            Thread.Sleep(100);

            if (MyDevice.BulkOutEndPt.XferData(ref buf_Xiu, ref len_Xiu) != true)
                MessageBox.Show("error", "修改采样率失败");
        }

        private void PianXuFangDa_Button_Click(object sender, EventArgs e)
        {
            int PianNum = Convert.ToInt16(PianXuanPian_ComboBox.Text);
            PianXuanFangDa(PianNum, Convert.ToInt16(PianXuanBeiShu_ComboBox.Text));
            //switch(PianXuanPian_ComboBox.Text)
            //{
            //    case "1":
            //        lvi_Array[temp_8].SubItems[3].Text = temp_88.ToString();
            //}
            for(int i = 0; i < 8; i++)
            {
                lvi_Array[(PianNum - 1) * 8 + i].SubItems[3].Text = PianXuanBeiShu_ComboBox.Text;
            }
        }

        private void DingZuSave_Button_Click(object sender, EventArgs e)
        {            
            if (DingZu_TextBox.Text == "")
                return;
            string temp_str = string.Format("{0:N1}", System.Convert.ToDouble(DingZu_TextBox.Text));
            DingZuValue = (int)(System.Convert.ToDouble(temp_str) * 1000 / 4);
            DingZuSave_Button.Text = "正在保存";
            Flag_recQueue_Save_DingZu = true;            
            recQueue_Save = new ConcurrentQueue<byte[]>();//清除上一次的数据，确保队列为空
            stopwatch_test.Restart();
        }

        public void DingZuSaveShuju(byte[] buf)
        {
            if (Flag_recQueue_Save_DingZu == false)
                return;
            if (DingZuValue > recQueue_Save.Count)
            {
                recQueue_Save.Enqueue(buf);
                
                //DingZuValue -= 4;
            }
            //else if (Flag_recQueue_Save_DingZu)
            else
            {
                long temp_stopwatch = stopwatch_test.ElapsedMilliseconds;
                System.Diagnostics.Debug.WriteLine("DingZu: {0}", temp_stopwatch);
                stopwatch_test.Reset();                
                Flag_recQueue_Save_DingZu = false;
                
                Action<bool> action = (x) =>
                {
                    MessageBox.Show("定组保存", "定组保存完成");
                    DingZuSave_Button.Text = "定组保存";
                };
                this.Invoke(action, true);
            }
        }

        
    }

    class Circle_Array<T>
    {
        public T[] Circle_Array_T = null;//实际数据的存放位置
        public int Index_Array_Save = 0;//当前存储的索引
        public int length = 0;//循环数组的长度
        public int Index_Array_Read = 0;//读取位置的索引，

        public Circle_Array(int length_param)
        {
            length = length_param;
            Circle_Array_T = new T[length];
        }

        public void AddItem(T param_int)
        {
            Circle_Array_T[Index_Array_Save] = param_int;
            Index_Array_Save++;
            if (Index_Array_Save >= length)
                Index_Array_Save = 0;
        }

        public void AddRange(T[] param_int_Array, int size)
        {
            for (int i = 0; i < size; i++)
            {
                Circle_Array_T[Index_Array_Save] = param_int_Array[i];
                Index_Array_Save++;
                if (Index_Array_Save >= length)
                    Index_Array_Save = 0;
            }
        }
        public T Circle_Read(ref int index)
        {
            index++;
            if (index >= length)
            {
                index = 0;
                return Circle_Array_T[length - 1];//最后一个数据
            }
            else
            {
                return Circle_Array_T[(index - 1)];
            }
        }
    }
}
