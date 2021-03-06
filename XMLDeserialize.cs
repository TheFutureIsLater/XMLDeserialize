using System;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

//TODO: deserialize Attributes
//TODO: deserialize Lists/Arrays

class XMLDeserialize : Form {
    OpenFileDialog OFD = new OpenFileDialog() { Filter = "XML | *.XML" };

    /// <summary>
    /// Build the window and components
    /// </summary>
    public XMLDeserialize() {
        // build a button1 that fills the whole window
        Button button1 = new Button() {
            Dock = System.Windows.Forms.DockStyle.Fill,
            Text = "Deserialize",
        };
        button1.Click += (sender, e) => {
            if (OFD.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {//Get file
                object test = Deserialize(OFD.FileName);//reads in the file and converts it to an object
                if (test != null)
                {
                    MessageBox.Show(objectToString(test), test.GetType().ToString());//puts the field name/value pairs into a message box.
                }
            }
        };
        // add button1 to the window
        this.Controls.Add(button1);
    }

    /// <summary>
    /// get the field values and children field values recursivly
    /// </summary>
    /// <param name="Input">Object to stringify</param>
    /// <returns>Returns all the field-value pairs for the given object as a single string!</returns>
    public static string objectToString(object Input) { return objectToString(Input, null, 0); }
    private static string objectToString(object Input, string partial, int lvl)
    {
        if (Input == null) { return "NULL"; }
        //if it's just a regular thing then is easy
        if (Input.GetType().IsPrimitive || Input is string)
        {
            return Input.ToString();
        }

        //brings in whatever we have so far, we will add to this.
        string output = partial;

        //check all the fields
        foreach (FieldInfo FI in Input.GetType().GetFields())
        {
            //if it has useful children, record this name and get the children information
            FieldInfo[] f = FI.FieldType.GetFields();
            if (FI.FieldType.IsPrimitive || FI.FieldType == typeof(string) || FI.FieldType == typeof(decimal) || FI.FieldType == typeof(DateTime))
            {
                output += new string('>', lvl) + FI.Name + " : " + FI.GetValue(Input) + "\r\n";
            }
            //If it is a collection poop out all the items
            else if (FI.GetValue(Input) is System.Collections.ICollection)
            {
                string part = new string('>', lvl) + FI.Name + " : \r\n";
                output += part;
                int Count = 0;
                foreach (object o in (FI.GetValue(Input) as System.Collections.ICollection))
                {
                    part = string.Format("{0} {1}[{2}]: \r\n", new string('>', lvl + 1), o.GetType(), Count++);
                    output += objectToString(o, part, lvl + 2);
                }
            }
            else
            {
                string part = new string('>', lvl) + FI.Name + " : \r\n";
                output += objectToString(FI.GetValue(Input), part, lvl + 1);
            }
        }

        foreach (PropertyInfo PI in Input.GetType().GetProperties())
        {
            if (PI.PropertyType.IsPrimitive || PI.PropertyType == typeof(string) || PI.PropertyType == typeof(decimal))
            {
                output += new string('>', lvl) + PI.Name + " : " + PI.GetValue(Input) + "\r\n";
            }
            //If it is a collection poop out all the items
            else if (PI.GetValue(Input) is System.Collections.ICollection)
            {
                string part = new string('>', lvl) + PI.Name + " : \r\n";
                output += part;
                int Count = 0;
                foreach (object o in (PI.GetValue(Input) as System.Collections.ICollection))
                {
                    part = string.Format("{0} {1}[{2}]: \r\n", new string('>', lvl + 1), o.GetType(), Count++);
                    output += objectToString(o, part, lvl + 2);
                }
            }
            else
            {
                string part = new string('>', lvl) + PI.Name + " : \r\n";
                output += objectToString(PI.GetValue(Input), part, lvl + 1);
            }

            //System.Windows.Forms.MessageBox.Show(temp);
        }

        //all done
        return output;
    }
 
    /// <summary>
    /// Convert a confusing XML file into a useful object!
    /// </summary>
    /// <param name="InPath">Path of xml file</param>
    /// <returns>Returns a brand new Object from the given XML path!</returns>
    public static object Deserialize(string InPath) {
        object Output = null;
        if (!InPath.ToUpper().EndsWith("XML")) { return null; }//Needs to be XML

        try {

            //read the xml document and put it all into a single string.
            System.Text.StringBuilder xmlContent = new System.Text.StringBuilder();
            using (System.IO.StreamReader SR = new System.IO.StreamReader(InPath)) {
                while (!SR.EndOfStream) {
                    xmlContent.Append(SR.ReadLine());
                }
            }
            //Convert XML string to an xmlDoc
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xmlContent.ToString());

            //Create a new type based on the xml document and the xmlRoot Tag name
            string XMLRootTagName = xmlDoc.DocumentElement.Name;
            Type newType = DeserializeType(XMLRootTagName, xmlDoc.SelectSingleNode(XMLRootTagName).ChildNodes);

            object testObject = Activator.CreateInstance(newType);
            try
            {
                //convert the document to an object
                XmlSerializer xs = new XmlSerializer(newType);
                Output = xs.Deserialize(new System.IO.StreamReader(InPath));
            }
            catch (Exception x1)
            {
                string message = x1.Message;

                while (x1.InnerException != null)
                {
                    x1 = x1.InnerException;
                    message += "\r\n" + x1.Message;
                }

                MessageBox.Show(message);
            }
        }
        catch (Exception x) {
            return "ERROR: " + x.Message;
        }

        //return the object
        return Output;
    }

    /// <summary>
    /// Build a type based on the given XML Nodes
    /// </summary>
    /// <param name="TypeName">Name for the type</param>
    /// <param name="XMLNodes">List of nodes</param>
    /// <returns>A brand new Type!</returns>
    public static Type DeserializeType(string TypeName, XmlNodeList XMLNodes)
    {

        // create a new assembly and module 
        AssemblyName assName = new AssemblyName(TypeName + "_Assembly");
        AssemblyBuilder assBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assName, AssemblyBuilderAccess.Run);
        ModuleBuilder modBuilder = assBuilder.DefineDynamicModule(TypeName + "_Module");

        // create a new type builder
        string nSpace = XMLNodes.Item(0).ParentNode.ParentNode.Name.Replace("#", "");
        TypeBuilder typBuilder = modBuilder.DefineType(string.Format("{0}.{1}", nSpace, TypeName), TypeAttributes.Public | TypeAttributes.Class);

        //check all the nodes
        foreach (XmlNode node in XMLNodes)
        {
            FieldBuilder field;

            string nodeName = node.Name;
            if (node.NodeType == XmlNodeType.Comment) { continue; }
            // Generate a private field
            if (node.InnerText != node.InnerXml)
            {//this will be true if it useful has child nodes that require building another type
                field = typBuilder.DefineField(nodeName, DeserializeType(nodeName, node.ChildNodes), FieldAttributes.Public);
            }
            else
            {//get the node information
                if (node.Attributes.Count > 0)
                {
                    field = typBuilder.DefineField(nodeName, DeserializeType(nodeName, node.Attributes), FieldAttributes.Public);
                }
                else
                {
                    double d;
                    if (double.TryParse(node.InnerText, out d))
                    {
                        field = typBuilder.DefineField(nodeName, typeof(double), FieldAttributes.Public);
                    }
                    else
                    {
                        field = typBuilder.DefineField(nodeName, typeof(string), FieldAttributes.Public);
                    }
                }
            }
        }
        //create and return new type
        return typBuilder.CreateType();//the type and all sub-types have been built

    }

    public static Type DeserializeType(string TypeName, XmlAttributeCollection XMLAttributes)
    {        
        // create a new assembly and module 
        AssemblyName assName = new AssemblyName(TypeName + "_Assembly");
        AssemblyBuilder assBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assName, AssemblyBuilderAccess.Run);
        ModuleBuilder modBuilder = assBuilder.DefineDynamicModule(TypeName + "_Module");

        // create a new type builder
        TypeBuilder typBuilder = modBuilder.DefineType(TypeName, TypeAttributes.Public | TypeAttributes.Class);

        foreach (XmlAttribute Attr in XMLAttributes)
        {
            Type[] req = { typeof(XmlAttribute) };
            Type[] opt = { };
            FieldBuilder field = typBuilder.DefineField(Attr.Name, typeof(string), req, opt, FieldAttributes.Public);
        }

        return typBuilder.CreateType();
    }
}
/// <summary>
/// Entry point to start the program
/// </summary>
public class Program {
    //Interop calls for hiding the console window
    [DllImport("kernel32.dll")]
    static extern IntPtr GetConsoleWindow();
    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [STAThread]
    static void Main() {
        ShowWindow(GetConsoleWindow(), 0);//Hide this silly console window, we have a GUI!

        Application.EnableVisualStyles();
        Application.Run(new XMLDeserialize());
    }
}
