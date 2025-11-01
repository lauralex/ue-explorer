namespace UEExplorer.UI.Dialogs;

partial class NativeFunctionEditor
{
    /// <summary>
    /// Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    /// Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        this.components = new System.ComponentModel.Container();
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(800, 450);
        this.Text = "NativeFunctionEditor";

        this.label1 = new System.Windows.Forms.Label();
        this.TextBoxName = new System.Windows.Forms.TextBox();
        this.label2 = new System.Windows.Forms.Label();
        this.NumericToken = new System.Windows.Forms.NumericUpDown();
        this.label3 = new System.Windows.Forms.Label();
        this.ComboBoxType = new System.Windows.Forms.ComboBox();
        this.label4 = new System.Windows.Forms.Label();
        this.NumericPrecedence = new System.Windows.Forms.NumericUpDown();
        this.ButtonOK = new System.Windows.Forms.Button();
        this.ButtonCancel = new System.Windows.Forms.Button();
        ((System.ComponentModel.ISupportInitialize)(this.NumericToken)).BeginInit();
        ((System.ComponentModel.ISupportInitialize)(this.NumericPrecedence)).BeginInit();
        this.SuspendLayout();
        // 
        // label1
        // 
        this.label1.AutoSize = true;
        this.label1.Location = new System.Drawing.Point(12, 15);
        this.label1.Name = "label1";
        this.label1.Size = new System.Drawing.Size(38, 13);
        this.label1.TabIndex = 0;
        this.label1.Text = "Name:";
        // 
        // TextBoxName
        // 
        this.TextBoxName.Location = new System.Drawing.Point(90, 12);
        this.TextBoxName.Name = "TextBoxName";
        this.TextBoxName.Size = new System.Drawing.Size(182, 20);
        this.TextBoxName.TabIndex = 1;
        // 
        // label2
        // 
        this.label2.AutoSize = true;
        this.label2.Location = new System.Drawing.Point(12, 40);
        this.label2.Name = "label2";
        this.label2.Size = new System.Drawing.Size(62, 13);
        this.label2.TabIndex = 2;
        this.label2.Text = "Byte Token:";
        // 
        // NumericToken
        // 
        this.NumericToken.Location = new System.Drawing.Point(90, 38);
        this.NumericToken.Maximum = new decimal(new int[] { 65535, 0, 0, 0 });
        this.NumericToken.Name = "NumericToken";
        this.NumericToken.Size = new System.Drawing.Size(182, 20);
        this.NumericToken.TabIndex = 3;
        // 
        // label3
        // 
        this.label3.AutoSize = true;
        this.label3.Location = new System.Drawing.Point(12, 67);
        this.label3.Name = "label3";
        this.label3.Size = new System.Drawing.Size(34, 13);
        this.label3.TabIndex = 4;
        this.label3.Text = "Type:";
        // 
        // ComboBoxType
        // 
        this.ComboBoxType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        this.ComboBoxType.FormattingEnabled = true;
        this.ComboBoxType.Location = new System.Drawing.Point(90, 64);
        this.ComboBoxType.Name = "ComboBoxType";
        this.ComboBoxType.Size = new System.Drawing.Size(182, 21);
        this.ComboBoxType.TabIndex = 5;
        // 
        // label4
        // 
        this.label4.AutoSize = true;
        this.label4.Location = new System.Drawing.Point(12, 94);
        this.label4.Name = "label4";
        this.label4.Size = new System.Drawing.Size(71, 13);
        this.label4.TabIndex = 6;
        this.label4.Text = "Precedence:";
        // 
        // NumericPrecedence
        // 
        this.NumericPrecedence.Location = new System.Drawing.Point(90, 92);
        this.NumericPrecedence.Maximum = new decimal(new int[] { 255, 0, 0, 0 });
        this.NumericPrecedence.Name = "NumericPrecedence";
        this.NumericPrecedence.Size = new System.Drawing.Size(182, 20);
        this.NumericPrecedence.TabIndex = 7;
        // 
        // ButtonOK
        // 
        this.ButtonOK.Location = new System.Drawing.Point(116, 127);
        this.ButtonOK.Name = "ButtonOK";
        this.ButtonOK.Size = new System.Drawing.Size(75, 23);
        this.ButtonOK.TabIndex = 8;
        this.ButtonOK.Text = "OK";
        this.ButtonOK.UseVisualStyleBackColor = true;
        this.ButtonOK.Click += new System.EventHandler(this.ButtonOK_Click);
        // 
        // ButtonCancel
        // 
        this.ButtonCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
        this.ButtonCancel.Location = new System.Drawing.Point(197, 127);
        this.ButtonCancel.Name = "ButtonCancel";
        this.ButtonCancel.Size = new System.Drawing.Size(75, 23);
        this.ButtonCancel.TabIndex = 9;
        this.ButtonCancel.Text = "Cancel";
        this.ButtonCancel.UseVisualStyleBackColor = true;
        this.ButtonCancel.Click += new System.EventHandler(this.ButtonCancel_Click);
        // 
        // NativeFunctionEditor
        // 
        this.AcceptButton = this.ButtonOK;
        this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.CancelButton = this.ButtonCancel;
        this.ClientSize = new System.Drawing.Size(284, 162);
        this.Controls.Add(this.ButtonCancel);
        this.Controls.Add(this.ButtonOK);
        this.Controls.Add(this.NumericPrecedence);
        this.Controls.Add(this.label4);
        this.Controls.Add(this.ComboBoxType);
        this.Controls.Add(this.label3);
        this.Controls.Add(this.NumericToken);
        this.Controls.Add(this.label2);
        this.Controls.Add(this.TextBoxName);
        this.Controls.Add(this.label1);
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.Name = "NativeFunctionEditor";
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        this.Text = "Native Function";
        ((System.ComponentModel.ISupportInitialize)(this.NumericToken)).EndInit();
        ((System.ComponentModel.ISupportInitialize)(this.NumericPrecedence)).EndInit();
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    #endregion

    private System.Windows.Forms.Label label1;
    private System.Windows.Forms.TextBox TextBoxName;
    private System.Windows.Forms.Label label2;
    private System.Windows.Forms.NumericUpDown NumericToken;
    private System.Windows.Forms.Label label3;
    private System.Windows.Forms.ComboBox ComboBoxType;
    private System.Windows.Forms.Label label4;
    private System.Windows.Forms.NumericUpDown NumericPrecedence;
    private System.Windows.Forms.Button ButtonOK;
    private System.Windows.Forms.Button ButtonCancel;
}
