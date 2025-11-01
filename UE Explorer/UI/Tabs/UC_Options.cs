using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using UEExplorer.Properties;
using UEExplorer.UI.Dialogs;
using UELib;
using UELib.Types;

namespace UEExplorer.UI.Tabs
{
    [ComVisible(false)]
    public partial class UC_Options : UserControl_Tab
    {
        public UC_Options()
        {
            InitializeComponent();
        }

        private void UC_Options_Load(object sender, EventArgs e)
        {
            Program.LoadConfig();
            CheckBox_SerObj.Checked = Program.Options.InitFlags.HasFlag(UnrealPackage.InitFlags.Deserialize);
            CheckBox_LinkObj.Checked = Program.Options.InitFlags.HasFlag(UnrealPackage.InitFlags.Link);

            foreach (string filePath in GetNativeTables())
            {
                ComboBox_NativeTable.Items.Add(Path.GetFileNameWithoutExtension(filePath));
            }

            ComboBox_NativeTable.SelectedIndex = ComboBox_NativeTable.Items.IndexOf(Program.Options.NTLPath);

            // Update the natives list view when a new table is selected.
            ComboBox_NativeTable.SelectedIndexChanged += ComboBox_NativeTable_OnSelectedIndexChanged;

            // Initial load of the natives list for the default selection.
            if (ComboBox_NativeTable.SelectedItem != null)
            {
                LoadNativesList(ComboBox_NativeTable.SelectedItem.ToString());
            }

            CheckBox_Version.Checked = Program.Options.bForceVersion;
            NumericUpDown_Version.Value = Program.Options.Version;
            CheckBox_LicenseeMode.Checked = Program.Options.bForceLicenseeMode;
            NumericUpDown_LicenseeMode.Value = Program.Options.LicenseeMode;
            SuppressComments.Checked = Program.Options.bSuppressComments;
            PreBeginBracket.Text = Program.Options.PreBeginBracket;
            PreEndBracket.Text = Program.Options.PreEndBracket;

            PathText.Text = Program.Options.UEModelAppPath;
            PathText_TextChanged(PathText, EventArgs.Empty);
            IndentionNumeric.Value = Program.Options.Indention;

            foreach (string enumElement in Enum.GetNames(typeof(PropertyType)))
            {
                if (enumElement == "StructOffset")
                {
                    continue;
                }

                VariableType.Items.Add(enumElement);
            }

            foreach (string pair in Program.Options.VariableTypes)
            {
                VariableTypesTree.Nodes.Add(new TreeNode(pair) { Tag = pair });
            }

            PreBeginBracket.TextChanged += PreBeginBracket_TextChanged;
            PreEndBracket.TextChanged += PreEndBracket_TextChanged;
            IndentionNumeric.ValueChanged += IndentionNumeric_ValueChanged;
            UpdateBracketPreview();
        }

        public static IEnumerable<string> GetNativeTables()
        {
            return Directory.GetFiles(Path.Combine(Application.StartupPath, "Native Tables"), "*"
                + NativesTablePackage.Extension);
        }

        private void Button_Save_Click(object sender, EventArgs e)
        {
            Program.Options.NTLPath = ComboBox_NativeTable.SelectedItem.ToString();
            Program.Options.InitFlags = UnrealPackage.InitFlags.All;

            if (!CheckBox_LinkObj.Checked)
            {
                Program.Options.InitFlags &= ~UnrealPackage.InitFlags.Link;
            }

            Program.Options.InitFlags |= UnrealPackage.InitFlags.Deserialize;

            if (!CheckBox_SerObj.Checked)
            {
                Program.Options.InitFlags &= ~UnrealPackage.InitFlags.Deserialize;
            }

            Program.Options.InitFlags |= UnrealPackage.InitFlags.RegisterClasses;

            Program.Options.bForceVersion = CheckBox_Version.Checked;
            Program.Options.Version = (ushort)NumericUpDown_Version.Value;

            Program.Options.bForceLicenseeMode = CheckBox_LicenseeMode.Checked;
            Program.Options.LicenseeMode = (ushort)NumericUpDown_LicenseeMode.Value;

            Program.Options.bSuppressComments = SuppressComments.Checked;
            UnrealConfig.SuppressComments = SuppressComments.Checked;
            Program.Options.UEModelAppPath = PathText.Text;

            Program.Options.PreBeginBracket = PreBeginBracket.Text;
            Program.Options.PreEndBracket = PreEndBracket.Text;
            UnrealConfig.PreBeginBracket = Program.ParseFormatOption(Program.Options.PreBeginBracket);
            UnrealConfig.PreEndBracket = Program.ParseFormatOption(Program.Options.PreEndBracket);

            Program.Options.Indention = (int)IndentionNumeric.Value;
            UnrealConfig.Indention = Program.ParseIndention(Program.Options.Indention);

            Program.Options.VariableTypes.Clear();
            foreach (TreeNode node in VariableTypesTree.Nodes)
            {
                Program.Options.VariableTypes.Add((string)node.Tag);
            }

            Program.CopyVariableTypes();

            Program.SaveConfig();
            MessageBox.Show(Resources.SAVE_SUCCESS, Resources.SAVED,
                MessageBoxButtons.OK, MessageBoxIcon.Information
            );
        }

        private static string FormatVariable(KeyValuePair<string, Tuple<string, PropertyType>> keyPair)
        {
            return keyPair.Value.Item1 + ":" + keyPair.Value.Item2;
        }

        private void PathButton_Click(object sender, EventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "UE Model View (umodel.exe)|umodel.exe"
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                PathText.Text = dialog.FileName;
            }
        }

        private void PathText_TextChanged(object sender, EventArgs e)
        {
            PathText.BackColor = File.Exists(PathText.Text)
                                 && Path.GetFileName(PathText.Text) == "umodel.exe"
                ? Color.Green
                : Color.Red;
        }

        private void VariableTypesTree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            var varTuple = Program.ParseVariable((string)e.Node.Tag);

            VariableTypeGroup.Text = (string)e.Node.Tag;
            VariableType.SelectedIndex = VariableType.Items.IndexOf(varTuple.Item3.ToString());

            VariableTypeGroup.Enabled = true;
            VariableType.Enabled = true;

            DeleteArrayType.Enabled = true;
        }

        private void VariableType_SelectedIndexChanged(object sender, EventArgs e)
        {
            var varData = Program.ParseVariable((string)VariableTypesTree.SelectedNode.Tag);
            VariableTypeGroup.Text = varData.Item2 + ":" + VariableType.SelectedItem;
        }

        private void VariableTypeGroup_TextChanged(object sender, EventArgs e)
        {
            VariableTypesTree.SelectedNode.Tag = VariableTypeGroup.Text;
            VariableTypesTree.SelectedNode.Text = (string)VariableTypesTree.SelectedNode.Tag;
        }

        private void NewArrayType_Click(object sender, EventArgs e)
        {
            var node = new TreeNode("Package.Class.Property:ObjectProperty")
                { Tag = "Package.Class.Property:ObjectProperty" };
            VariableTypesTree.Nodes.Add(node);
            VariableTypesTree.SelectedNode = node;
        }

        private void DeleteArrayType_Click(object sender, EventArgs e)
        {
            if (VariableTypesTree.SelectedNode == null)
            {
                return;
            }

            VariableTypesTree.Nodes.Remove(VariableTypesTree.SelectedNode);

            if (VariableTypesTree.Nodes.Count == 0)
            {
                //VariableTypeGroup.Text = String.Empty;
                VariableTypeGroup.Enabled = false;
                VariableType.Enabled = false;
                DeleteArrayType.Enabled = false;
            }
        }

        private void PreBeginBracket_TextChanged(object sender, EventArgs e)
        {
            UpdateBracketPreview();
        }

        private void PreEndBracket_TextChanged(object sender, EventArgs e)
        {
            UpdateBracketPreview();
        }

        private void IndentionNumeric_ValueChanged(object sender, EventArgs e)
        {
            UpdateBracketPreview();
        }

        private void UpdateBracketPreview()
        {
            string preBB = UnrealConfig.PreBeginBracket;
            string preEB = UnrealConfig.PreEndBracket;
            string preTABS = UnrealConfig.Indention;
            UnrealConfig.PreBeginBracket = Program.ParseFormatOption(PreBeginBracket.Text);
            UnrealConfig.PreEndBracket = Program.ParseFormatOption(PreEndBracket.Text);
            UnrealConfig.Indention = Program.ParseIndention((int)IndentionNumeric.Value);

            UDecompilingState.ResetTabs();

            string output = "function Preview()" + UnrealConfig.PrintBeginBracket();
            UDecompilingState.AddTab();
            output += "\r\n" + UDecompilingState.Tabs + "if( true )" + UnrealConfig.PrintBeginBracket();
            UDecompilingState.AddTab();
            output += "\r\n" + UDecompilingState.Tabs + "[CODE]";
            UDecompilingState.RemoveTab();
            output += UnrealConfig.PrintEndBracket();
            UDecompilingState.RemoveTab();
            output += UnrealConfig.PrintEndBracket();
            BracketPreview.Text = output;

            UnrealConfig.PreBeginBracket = preBB;
            UnrealConfig.PreEndBracket = preEB;
            UnrealConfig.Indention = preTABS;
        }

        private void CheckBox_Version_CheckedChanged(object sender, EventArgs e)
        {
            NumericUpDown_Version.Enabled = CheckBox_Version.Checked;
        }

        private void CheckBox_LicenseeMode_CheckedChanged(object sender, EventArgs e)
        {
            NumericUpDown_LicenseeMode.Enabled = CheckBox_LicenseeMode.Checked;
        }

        private void ComboBox_NativeTable_OnSelectedIndexChanged(object sender, EventArgs e)
        {
            if (ComboBox_NativeTable.SelectedItem == null)
            {
                return;
            }

            LoadNativesList(ComboBox_NativeTable.SelectedItem.ToString());
        }

        private void LoadNativesList(string nativeTableName)
        {
            NativesListView.Items.Clear();
            if (string.IsNullOrEmpty(nativeTableName))
            {
                return;
            }

            string nativeTablePath = Path.Combine(Application.StartupPath, "Native Tables",
                nativeTableName + NativesTablePackage.Extension);
            if (!File.Exists(nativeTablePath))
            {
                return;
            }

            try
            {
                var nativesPackage = new NativesTablePackage();
                using (var stream = new FileStream(nativeTablePath, FileMode.Open, FileAccess.Read))
                {
                    nativesPackage.Deserialize(stream);
                }

                NativesListView.BeginUpdate();
                foreach (var nativeItem in nativesPackage.NativeTableList)
                {
                    var item = new ListViewItem(nativeItem.Name);
                    item.SubItems.Add(nativeItem.ByteToken.ToString());
                    item.SubItems.Add(nativeItem.Type.ToString());
                    item.SubItems.Add(nativeItem.OperPrecedence.ToString());
                    NativesListView.Items.Add(item);
                }

                NativesListView.EndUpdate();
            }
            catch (Exception ex)
            {
                MessageBox.Show($@"Error loading native table '{nativeTableName}':\r\n{ex.Message}",
                    @"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ButtonAddNative_Click(object sender, EventArgs e)
        {
            using (var editor = new NativeFunctionEditor())
            {
                if (editor.ShowDialog() == DialogResult.OK)
                {
                    var nativeItem = editor.NativeItem;
                    var item = new ListViewItem(nativeItem.Name);
                    item.SubItems.Add(nativeItem.ByteToken.ToString());
                    item.SubItems.Add(nativeItem.Type.ToString());
                    item.SubItems.Add(nativeItem.OperPrecedence.ToString());
                    NativesListView.Items.Add(item);
                }
            }
        }

        private void ButtonRemoveNative_Click(object sender, EventArgs e)
        {
            if (NativesListView.SelectedItems.Count == 0)
            {
                MessageBox.Show(@"Please select a native function to remove.", @"No Selection", MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show(@"Are you sure you want to remove the selected native function?", @"Confirm Deletion",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                foreach (ListViewItem item in NativesListView.SelectedItems)
                {
                    NativesListView.Items.Remove(item);
                }
            }
        }

        private void ButtonSaveNatives_Click(object sender, EventArgs e)
        {
            if (ComboBox_NativeTable.SelectedItem == null)
            {
                MessageBox.Show(@"Cannot save because no native table is selected.", @"Error", MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            string nativeTableName = ComboBox_NativeTable.SelectedItem.ToString();
            string nativeTablePath = Path.Combine(Application.StartupPath, "Native Tables",
                nativeTableName + NativesTablePackage.Extension);

            var package = new NativesTablePackage { NativeTableList = new List<NativeTableItem>() };

            foreach (ListViewItem item in NativesListView.Items)
            {
                try
                {
                    var nativeItem = new NativeTableItem
                    {
                        Name = item.SubItems[0].Text,
                        ByteToken = int.Parse(item.SubItems[1].Text),
                        Type = (FunctionType)Enum.Parse(typeof(FunctionType), item.SubItems[2].Text),
                        OperPrecedence = byte.Parse(item.SubItems[3].Text)
                    };
                    package.NativeTableList.Add(nativeItem);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($@"Failed to parse item '{item.Text}':\r\n{ex.Message}", @"Parsing Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            try
            {
                using (var stream = new FileStream(nativeTablePath, FileMode.Create, FileAccess.Write))
                {
                    package.Serialize(stream);
                }

                MessageBox.Show($@"Successfully saved '{nativeTableName}{NativesTablePackage.Extension}'.",
                    @"Save Successful", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($@"Error saving native table:\r\n{ex.Message}", @"Save Error", MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void NativesListView_DoubleClick(object sender, EventArgs e)
        {
            if (NativesListView.SelectedItems.Count == 0)
                return;

            var selectedItem = NativesListView.SelectedItems[0];
            var nativeToEdit = new NativeTableItem
            {
                Name = selectedItem.SubItems[0].Text,
                ByteToken = int.Parse(selectedItem.SubItems[1].Text),
                Type = (FunctionType)Enum.Parse(typeof(FunctionType), selectedItem.SubItems[2].Text),
                OperPrecedence = byte.Parse(selectedItem.SubItems[3].Text)
            };

            using (var editor = new NativeFunctionEditor(nativeToEdit))
            {
                if (editor.ShowDialog() == DialogResult.OK)
                {
                    var updatedItem = editor.NativeItem;
                    selectedItem.SubItems[0].Text = updatedItem.Name;
                    selectedItem.SubItems[1].Text = updatedItem.ByteToken.ToString();
                    selectedItem.SubItems[2].Text = updatedItem.Type.ToString();
                    selectedItem.SubItems[3].Text = updatedItem.OperPrecedence.ToString();
                }
            }
        }
    }
}
