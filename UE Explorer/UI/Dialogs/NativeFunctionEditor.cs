using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using UELib;

namespace UEExplorer.UI.Dialogs;
public partial class NativeFunctionEditor : Form
{
    public NativeTableItem NativeItem { get; private set; }

    public NativeFunctionEditor(NativeTableItem item = null)
    {
        InitializeComponent();
        NativeItem = item ?? new NativeTableItem();

        // Populate FunctionType ComboBox
        ComboBoxType.DataSource = Enum.GetValues(typeof(FunctionType));

        if (item != null)
        {
            Text = @"Edit Native Function";
            TextBoxName.Text = NativeItem.Name;
            NumericToken.Value = NativeItem.ByteToken;
            ComboBoxType.SelectedItem = NativeItem.Type;
            NumericPrecedence.Value = NativeItem.OperPrecedence;
        }
        else
        {
            Text = @"Add Native Function";
        }
    }

    private void ButtonOK_Click(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TextBoxName.Text))
        {
            MessageBox.Show(@"Name cannot be empty.", @"Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        NativeItem.Name = TextBoxName.Text;
        NativeItem.ByteToken = (int)NumericToken.Value;
        NativeItem.Type = (FunctionType)ComboBoxType.SelectedItem;
        NativeItem.OperPrecedence = (byte)NumericPrecedence.Value;

        DialogResult = DialogResult.OK;
        Close();
    }

    private void ButtonCancel_Click(object sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }
}
