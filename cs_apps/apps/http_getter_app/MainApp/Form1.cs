using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace apps.http_getter_app.MainApp
{
    public partial class Form1 : Form
    {
        public HttpObserver httpObserver;
        public Form1()
        {
            InitializeComponent();
            this.httpObserver = new HttpObserver();
            this.FormClosing += new FormClosingEventHandler(Form1_FormClosing);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Console.WriteLine("button1_Click() START");
            // this.httpObserver.RequestOneTime(); // 単品
            this.httpObserver.RequestMultipleTime(); // 複数
            Console.WriteLine("button1_Click END");
        }

        // Formが閉じられるときに呼び出されるイベントハンドラ
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Console.WriteLine("Form1_FormClosing() START");
            if (this.httpObserver != null)
            {
                this.httpObserver.Dispose();
                this.httpObserver = null;
            }
            Console.WriteLine("Form1_FormClosing() END");
        }
    }
}
