using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace NoisetteDeobfuscator
{
    public partial class Form1 : Form
    {
      
        #region Variables
        string directoryName = "";
        string filePath = "";
        static ModuleDefMD module = null;
        public Thread thr;
        #endregion


        public Form1()
        {
            CheckForIllegalCrossThreadCalls = false;
            InitializeComponent();
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            try
            {
                OpenFileDialog open = new OpenFileDialog();
                open.Filter = "Executable Files|*.exe|DLL Files |*.dll";
                if (open.ShowDialog() == DialogResult.OK)
                {
                    module = ModuleDefMD.Load(open.FileName);
                    filePath = open.FileName;
                    label4.Text = "Loaded !";
                    label4.ForeColor = Color.Lime;
                    listBox1.Items.Clear();
                    label3.Visible = false;
                    listBox1.Items.Add("File is loaded !");
                    listBox1.Items.Add("EntryPoint MDToken : 0x" + module.EntryPoint.MDToken.ToString());
                }
            }
            catch (Exception ex)
            {
                filePath = "";
                module = null;
                MessageBox.Show(ex.Message, "Error !", MessageBoxButtons.OK, MessageBoxIcon.Error);
                label4.Text = "Not Loaded !";
                label4.ForeColor = Color.Lime;
                label3.Visible = true;

            }
        }

        #region Save
        static void SaveAssembly()
        {
            var writerOptions = new NativeModuleWriterOptions(module, true);
            writerOptions.Logger = DummyLogger.NoThrowInstance;
            writerOptions.MetadataOptions.Flags = (MetadataFlags.PreserveTypeRefRids | MetadataFlags.PreserveTypeDefRids | MetadataFlags.PreserveFieldRids | MetadataFlags.PreserveMethodRids | MetadataFlags.PreserveParamRids | MetadataFlags.PreserveMemberRefRids | MetadataFlags.PreserveStandAloneSigRids | MetadataFlags.PreserveEventRids | MetadataFlags.PreservePropertyRids | MetadataFlags.PreserveTypeSpecRids | MetadataFlags.PreserveMethodSpecRids | MetadataFlags.PreserveStringsOffsets | MetadataFlags.PreserveUSOffsets | MetadataFlags.PreserveBlobOffsets | MetadataFlags.PreserveAll | MetadataFlags.AlwaysCreateGuidHeap | MetadataFlags.PreserveExtraSignatureData | MetadataFlags.KeepOldMaxStack);
            module.NativeWrite(Path.GetDirectoryName(module.Location) + @"\" + Path.GetFileNameWithoutExtension(module.Location) + "_deobf.exe", writerOptions);
        }

        #endregion

        #region Github
        private void Label2_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/Rhotav");
        }
        #endregion

        private void Button2_Click(object sender, EventArgs e)
        {
            if (filePath != string.Empty && module != null)
            {
                thr = new Thread(new ThreadStart(CodeBlock));
                thr.Start();
            }
        }
        public void CodeBlock()
        {
            try
            {
                IntegrityCheckNop();
                Renamer();
                StringInliner();
                ConstantInline();
                cctorCleaner();
                SaveAssembly();
                listBox1.Items.Add("Compeleted and Saved !");

            }
            catch (Exception ex)
            {
                listBox1.Items.Add(ex.Message);
            }
        }
        public void StringInliner()
        {
            StringLister();
            foreach(TypeDef type in module.Types)
            {
                foreach(MethodDef method in type.Methods)
                {
                    if (!method.HasBody) continue;
                    if (method.IsConstructor) continue;


                    for (int i = 0; i < method.Body.Instructions.Count; i++)
                    {
                        if (method.Body.Instructions[i].OpCode == OpCodes.Ldsfld)
                        {
                            string value = StringValueFind(method.Body.Instructions[i].Operand.ToString());
                            if (value != "")
                            {
                                method.Body.Instructions[i].OpCode = OpCodes.Ldstr;
                                method.Body.Instructions[i].Operand = value;
                            }
                        }
                    }

                }
            }
            listBox1.Items.Add("Strings are inlined.");
        }
        public void cctorCleaner()
        {
            MethodDef cctor = module.GlobalType.FindOrCreateStaticConstructor();

            for (int i = 0; i < cctor.Body.Instructions.Count; i++)
            {
                if (cctor.Body.Instructions[i].OpCode == OpCodes.Ldstr &&
                    cctor.Body.Instructions[i + 1].OpCode == OpCodes.Stsfld)
                {
                    cctor.Body.Instructions.RemoveAt(i);
                    cctor.Body.Instructions.RemoveAt(i);
                    i--;
                }
            }
            listBox1.Items.Add("CCTOR is cleaned.");
        }

        public void ConstantInline()
        {
            foreach(TypeDef type in module.Types)
            {
                foreach(MethodDef method in type.Methods)
                {
                    if (!method.HasBody) continue;

                    for(int i = 0; i < method.Body.Instructions.Count; i++)
                    {
                        if(method.Body.Instructions[i].OpCode == OpCodes.Ldc_I4 && 
                            method.Body.Instructions[i + 6].OpCode == OpCodes.Br_S)
                        {
                            method.Body.Instructions.RemoveAt(i);
                            method.Body.Instructions.RemoveAt(i);
                            method.Body.Instructions.RemoveAt(i);
                            method.Body.Instructions.RemoveAt(i);
                            method.Body.Instructions.RemoveAt(i);
                        }
                    }
                }
            }
            listBox1.Items.Add("Constants are inlined.");
        }
        public void Renamer()
        {
            int form = 1;
            int method = 0;
            int xclass = 1;
            int field = 0;
            
            foreach(TypeDef type in module.Types)
            {
                if (type.Name == "<Module>") continue;
                if (type.IsRuntimeSpecialName) continue;
                if (type.BaseType.ToString().Contains("Forms.Form"))
                {
                    type.Name = "Form_" + form.ToString();
                    form++;

                }
                else
                {
                    type.Name = "Class_" + xclass.ToString();
                    xclass++;
                }

                foreach (MethodDef methodDef in type.Methods)
                {
                    if (methodDef.IsConstructor && methodDef.IsRuntimeSpecialName) continue;

                        methodDef.Name = "Method_" + method.ToString();
                    method++;
                }

                foreach(FieldDef fieldDef in type.Fields)
                {
                    if (fieldDef.IsRuntimeSpecialName) continue;

                    fieldDef.Name = "Field_" + field.ToString();
                    field++;
                }

            }
            int result = form + xclass + field + method;
            listBox1.Items.Add(result.ToString() + " names changed.");

        }
        public string StringValueFind(string oper)
        {
            for(int i = 0; i < operList.Count; i++)
            {
                if(oper == operList[i])
                {
                    return strList[i];
                }
            }
            return "";

        }


        List<string> strList = new List<string>();
        List<string> operList = new List<string>();

        public void StringLister()
        {
            MethodDef cctor = module.GlobalType.FindOrCreateStaticConstructor();

            for (int i = 0; i < cctor.Body.Instructions.Count; i++)
            {
                if (cctor.Body.Instructions[i].OpCode == OpCodes.Ret) break;
                if(cctor.Body.Instructions[i].OpCode == OpCodes.Ldstr &&
                    cctor.Body.Instructions[i + 1].OpCode == OpCodes.Stsfld)
                {
                    strList.Add(cctor.Body.Instructions[i].Operand.ToString());
                    operList.Add(cctor.Body.Instructions[i + 1].Operand.ToString());

                    //cctor.Body.Instructions.RemoveAt(i);
                    //cctor.Body.Instructions.RemoveAt(i);

                    //i--;

                }

            }

            
        }
        public void IntegrityCheckNop()
        {
            foreach(TypeDef type in module.Types)
            {
                foreach(MethodDef method in type.Methods)
                {
                    if (!method.HasBody) continue;
                    if (!method.IsConstructor) continue;
                    method.Body.Instructions.RemoveAt(0);
                }
            }
            listBox1.Items.Add("Integrity Check is removed.");
        }

        #region DragDrop
        private void ListBox1_DragEnter(object sender, DragEventArgs e)
        {
            try
            {
                Array array = (Array)e.Data.GetData(DataFormats.FileDrop);
                if (array != null)
                {
                    string text = array.GetValue(0).ToString();
                    int num = text.LastIndexOf(".");
                    if (num != -1)
                    {
                        string text2 = text.Substring(num);
                        text2 = text2.ToLower();
                        if (text2 == ".exe" || text2 == ".dll")
                        {
                            Activate();
                            int num2 = text.LastIndexOf("\\");
                            if (num2 != -1)
                            {
                                directoryName = text.Remove(num2, text.Length - num2);
                            }
                            if (directoryName.Length == 2)
                            {
                                directoryName += "\\";
                            }
                            module = ModuleDefMD.Load(text);
                            filePath = text;
                            label4.Text = "Loaded !";
                            label4.ForeColor = Color.Lime;
                            label3.Visible = false;
                            listBox1.Items.Clear();
                            listBox1.Items.Add("File is loaded !");
                            listBox1.Items.Add("EntryPoint MDToken : 0x" + module.EntryPoint.MDToken.ToString());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                filePath = "";
                module = null;
                MessageBox.Show(ex.Message, "Error !", MessageBoxButtons.OK, MessageBoxIcon.Error);
                label4.Text = "Not Loaded !";
                label4.ForeColor = Color.Red;

            }
        }

        private void ListBox1_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }
        #endregion
    }
}
