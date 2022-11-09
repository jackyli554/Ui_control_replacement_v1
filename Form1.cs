using MySqlConnector;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace Ui_control_replacement_v1
{

    public partial class Form1 : Form
    {
        bool db_connected = false;
        #region DB connection credential
        MySqlConnection con = new MySqlConnection();
        String Server = "10.4.0.200";
        String uid = "new";
        String pwd = "1234";
        String starting_db = "IPS";
        #endregion
        bool on_brightnessbar = false;
        List<Label> temp_label_list = new List<Label>();
        List<Label> status_label_list = new List<Label>();
        List<Label> ext_label_list = new List<Label>();
        string sys_stat;

        int target_brightness;
        int brightness_read=-1;
        List<int> temp_list = new List<int>();
        List<string> EXT_status_list = new List<string>();
        List<string> status_list = new List<string>();
        bool pending_change = false;
        public Form1()
        {

            InitializeComponent();
            init_dbcon();
            preload_ui();
            Thread t1 = new Thread(DBfetch_display);//fetching from db will be done by another thread to prevent unresponsive gui every 5s
            t1.Start();

        }
        void init_dbcon()   //initalize a sqldb connection
        {
            try
            {
                MySqlConnectionStringBuilder con_str = new MySqlConnectionStringBuilder
                {
                    Server = Server,
                    UserID = uid,
                    Password = pwd,
                    Database = starting_db
                };
                con.ConnectionString = con_str.ConnectionString;
                con.Open();
                Trace.WriteLine("Connected to the database!");
                db_connected = true;
                // return true;
            }
            catch (MySqlException ex)
            {
                MessageBox.Show(ex.Message + "," + ex.ErrorCode);
                db_connected = false;
                //return false;
            }
        }
        bool db_readall()   //read all temp/each board status/controller status and save the result at temp_list, status_list,sys_stat
        {
            while (!db_connected)
            {
                init_dbcon();
            }
            try
            {
                if (db_connected == true)
                {

                    var temp_read_list = new List<int>();
                    var status_read_list = new List<string>();
                    var ext_stat_list = new List<string>();
                    MySqlCommand cmd = new MySqlCommand();
                    cmd.Connection = con;
                    cmd.CommandText = "select * from LED_INTERNAL_TEMP";
                    MySqlDataReader myreader = cmd.ExecuteReader();
                    while (myreader.Read())
                    {
                        temp_read_list.Add(myreader.GetInt32(1));
                    }
                    Trace.WriteLine("Read all temp completed!");
                    myreader.Close();
                    cmd.CommandText = "select * from LED_INTERNAL_TEMP";
                    myreader = cmd.ExecuteReader();
                    while (myreader.Read())
                    {
                        int res = myreader.GetInt32(2);
                        if(res == 1)
                        {
                            status_read_list.Add("OK");
                        }
                        else
                        {
                            status_read_list.Add("ABNORMAL");
                        }
                    }
                    Trace.WriteLine("Read all status completed!");

                    myreader.Close();
                    cmd.CommandText = "select * from STATUS";
                    myreader = cmd.ExecuteReader();
                    while (myreader.Read())
                    {
                        sys_stat = myreader.GetString(0);
                    }
                    Trace.WriteLine("Read system status completed!");
                    myreader.Close();
                    cmd.CommandText = "SELECT * from LED_EXTERNAL_TEMP";
                    myreader = cmd.ExecuteReader();
                    while(myreader.Read())
                    {
                        
                       // EXT_status_list.Add((int)myreader.GetFloat(1));
                        int res = myreader.GetInt32(2);
                        if (res == 1)
                        {
                            ext_stat_list.Add("OK");
                        }
                        else
                        {
                            ext_stat_list.Add("ABNORMAL");
                        }
                       
                    }
                    myreader.Close();
                    Trace.WriteLine("Read EXT_TEMP DONE");
                    temp_list = temp_read_list;
                    status_list = status_read_list;
                    EXT_status_list = ext_stat_list;
                    cmd.CommandText = "select * from BRIGHTNESS";
                    myreader = cmd.ExecuteReader();
                    while (myreader.Read())
                    {
                        brightness_read = (int)myreader.GetFloat(0);
                    }
                    myreader.Close();
                    Trace.WriteLine("Read Brightness completed!");
                    return true;
                    
                }
                else return false;
            }
            catch (MySqlException ex)
            {
                MessageBox.Show("DB Exception! " + ex.ErrorCode + " , " + ex.Message);
                db_connected = false;
                return false;
            }
        }
        void preload_ui()//bind the label of temp and status to temp_label_list and status_label_list
        {
            var groupbox_list = this.Controls.OfType<GroupBox>().ToList();
            groupbox_list.Reverse();
            foreach (var item in groupbox_list)
            {
                Trace.WriteLine(groupbox_list.IndexOf(item).ToString() + "  " + item.Name);
                Trace.WriteLine("control count = " + item.Controls.Count.ToString());
                if (item.Controls.Count > 0)
                {
                    temp_label_list.Add((Label)item.Controls[0]);
                    status_label_list.Add((Label)item.Controls[1]);
                }
            }
            var table = tableLayoutPanel1.Controls;
            for(int i=table.Count-1;i>=0;i--)
            {
                if(i%2==0)
                {
                    ext_label_list.Add((Label)table[i]);
                    table[i].Text = "Not initalized";
                }
            }
            
            for (int i = 0; i < 71; i++)
            {
                temp_label_list[i].Text = "Not";
                status_label_list[i].Text = "initalized";
            }
        }
        void update_ui()//update fetched temp/board status/controller status to the gui.
        {
            for(int i=0;i<71;i++)
            {
                AppendTextBox(temp_list[i].ToString(), temp_label_list[i]);
                AppendTextBox(status_list[i].ToString(), status_label_list[i]);
            }
            Trace.WriteLine("Applied new data to ITEMP and STATUS");
            AppendTextBox(sys_stat, sys_stat_label);
            AppendTextBox(brightness_read.ToString(), Current_brightness_label);
            for(int i=0;i<ext_label_list.Count;i++)
            {
                AppendTextBox(EXT_status_list[i], ext_label_list[i]);
            }
         
        }
        void DBfetch_display()  // background task to fetched from db and apply to gui every 5s
        {
            while (true)
            {
                if(db_connected == false)
                {
                    init_dbcon();
                }
                while (db_connected)
                {
                    if (db_readall())
                    {
                        Trace.WriteLine("db_readall in update ui completed!");
                        dump_content_value();
                        update_ui();
                    }
                    else
                    {
                        Trace.WriteLine("Read info from db failed!");
                    }
                    Thread.Sleep(5000);
                }
                Trace.WriteLine("Disconnected!");
            }
        }
        #region debug_helper
        public void dump_label_text()   // helper function to dump label name and content into trace
        {
            Trace.WriteLine("Dumping the temp label");
            Trace.WriteLine("The count of temp label = " + temp_label_list.Count);
            foreach (Label item in temp_label_list)
            {
                Trace.Write(temp_label_list.IndexOf(item) + "   =");
                Trace.WriteLine(item.Name);
            }
            Trace.WriteLine("Dumping the status label");
            Trace.WriteLine("The count of status label = " + status_label_list.Count);
            foreach (Label item in status_label_list)
            {
                Trace.Write(status_label_list.IndexOf(item) + "   =");
                Trace.WriteLine(item.Name);
            }
        }
        public void dump_content_value() // helper function to dump fetched db content into trace
        {
            Trace.WriteLine("The count of temp_list = " + temp_list.Count);
            Trace.WriteLine("dumping content of temp_list");
            for(int i=0;i<71;i++)
            {
                var item = temp_list[i];
                Trace.WriteLine(String.Format("Index = {0}   Value={1}", temp_list.IndexOf(item), item));
            }
            Trace.WriteLine("The count of status_list = " + status_list.Count);
            Trace.WriteLine("dumping content of status_list");

            for (int i = 0; i < 71; i++)
            {
                var item = status_list[i];
                Trace.WriteLine(String.Format("Index = {0}   Value={1}", status_list.IndexOf(item), item));
            }
        }
        #endregion
        public void AppendTextBox(string value,Label label) //function for cross-thread gui updating
        {
            if (InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate
                {
                    AppendTextBox(value, label);
                });
                    return;
            }
            label.Text = value;
        }
        bool updatedb_brightness(int brightness) //update db brightness from user input
        {
            try
            {
                while (!db_connected)
                {
                    init_dbcon();
                }
                if (db_connected == true)
                {
                    MySqlCommand cmd = new MySqlCommand("UPDATE BRIGHTNESS set brightness = " + brightness.ToString());
                    cmd.Connection = con;
                    return (cmd.ExecuteNonQuery() == 1);
                }
                else
                {
                    //Trace.WriteLine()
                    return false;
                }
            }
            catch(MySqlException ex)
            {
                MessageBox.Show(ex.Message+" , "+ ex.ErrorCode);
                return false;
            }
        }

        private void Brightness_bar_Scroll(object sender, EventArgs e) //do when user using the brightness bar
        {
            on_brightnessbar = true;
            pending_change = true;
        }

        private void Brightness_bar_MouseUp(object sender, MouseEventArgs e)    //do when user has done brightness input and will now send the value to db
        {
            if(on_brightnessbar)
            {
                on_brightnessbar = false;
                target_brightness = Brightness_bar.Value;
                MessageBox.Show("will be setting brightness to " + target_brightness.ToString());
                Trace.WriteLine("setting brightness to " + target_brightness.ToString());
                if(updatedb_brightness(target_brightness) == true)
                {
                    MessageBox.Show("Write Brightness to db successful!");
                }
                else
                {
                    MessageBox.Show("Write Brightness to db failed!");
                    //the current brightness value is inaccurate!
                }
            }
        }

        private void Refresh_all_btn_Click(object sender, EventArgs e)
        {
            db_readall();
        }



        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                con.Close();
                System.Environment.Exit(0);
            }
            catch
            {
                //nothing to do
            }
        }
    }

}
