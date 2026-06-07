using System;
using System.Drawing;
using System.Windows.Forms;
using KingdomBlind_CSharp.Audio;
using KingdomBlind_CSharp.Models;
using KingdomBlind_CSharp.Systems;

namespace KingdomBlind_CSharp.UI
{
    public class RenameForm : Form
    {
        public string InputText { get; private set; }
        private TextBox txtInput;
        private Button btnOk;
        private readonly INarrationService narration;
        private readonly GameState state;
        private readonly SapiEngine sapi;
        
        public RenameForm(string title, string prompt, string defaultName, SapiEngine sapi)
            : this(title, prompt, defaultName, null, null, sapi)
        {
        }

        public RenameForm(string title, string prompt, string defaultName, INarrationService narration, GameState state)
            : this(title, prompt, defaultName, narration, state, null)
        {
        }

        private RenameForm(string title, string prompt, string defaultName, INarrationService narration, GameState state, SapiEngine sapi)
        {
            this.narration = narration;
            this.state = state;
            this.sapi = sapi;

            this.Text = title;
            this.Size = new Size(400, 200);
            this.StartPosition = FormStartPosition.CenterParent;
            this.RightToLeft = RightToLeft.Yes;
            this.RightToLeftLayout = true;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            
            Label lbl = new Label();
            lbl.Text = prompt;
            lbl.Location = new Point(20, 20);
            lbl.AutoSize = true;
            lbl.Font = new Font("Arial", 12);
            this.Controls.Add(lbl);
            
            txtInput = new TextBox();
            txtInput.Text = defaultName;
            txtInput.Location = new Point(20, 60);
            txtInput.Size = new Size(340, 30);
            txtInput.Font = new Font("Arial", 12);
            txtInput.AccessibleName = "أدخل الاسم الجديد";
            txtInput.AccessibleRole = AccessibleRole.Text;
            txtInput.GotFocus += (s, e) => Speak("أدخل الاسم الجديد: " + txtInput.Text);
            this.Controls.Add(txtInput);
            
            btnOk = new Button();
            btnOk.Text = "تأكيد التغيير";
            btnOk.AccessibleName = "تأكيد التغيير";
            btnOk.Location = new Point(130, 100);
            btnOk.Size = new Size(120, 40);
            btnOk.Click += (s, e) => { 
                InputText = string.IsNullOrWhiteSpace(txtInput.Text) ? defaultName : txtInput.Text; 
                this.DialogResult = DialogResult.OK; 
                this.Close(); 
            };
            btnOk.GotFocus += (s, e) => Speak("تأكيد التغيير");
            this.Controls.Add(btnOk);
            
            this.AcceptButton = btnOk;
            this.ActiveControl = txtInput;
        }

        private void Speak(string text)
        {
            if (narration != null && state != null)
                narration.Speak(state, text);
            else if (sapi != null && sapi.IsEnabled)
                sapi.Speak(text);
        }
    }
}
