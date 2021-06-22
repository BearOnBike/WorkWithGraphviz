using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace WorkWithGraphViz.utils
{
    public static class ClsForInteractionWithGraphviz
    {
        //the entire bin folder of graphviz project is needed
        private const string LIB_GVC = @"C:\Program Files (x86)\Graphviz2.38\bin\gvc.dll";
        private const string LIB_GRAPH = @"C:\Program Files (x86)\Graphviz2.38\bin\cgraph.dll";
        private const int SUCCESS = 0;

        [DllImport(LIB_GVC)]
        private static extern IntPtr gvContext();

        [DllImport(LIB_GVC)]
        private static extern int gvFreeContext(IntPtr gvc);

        [DllImport(LIB_GRAPH)]
        private static extern IntPtr agmemread(string data);

        [DllImport(LIB_GRAPH)]
        private static extern void agclose(IntPtr g);

        [DllImport(LIB_GVC)]
        private static extern int gvLayout(IntPtr gvc, IntPtr g, string engine);

        [DllImport(LIB_GVC)]
        private static extern int gvFreeLayout(IntPtr gvc, IntPtr g);

        [DllImport(LIB_GVC)]
        private static extern int gvRenderFilename(IntPtr gvc, IntPtr g,
            string format, string fileName);

        [DllImport(LIB_GVC)]
        private static extern int gvRenderData(IntPtr gvc, IntPtr g,
            string format, out IntPtr result, out int length);

        public static byte[] RenderImage(string source, string layout, string format)
        {
            IntPtr gvc = IntPtr.Zero;
            IntPtr g = IntPtr.Zero;
            try
            {
                // Create a Graphviz context 
                gvc = gvContext();
                if (gvc == IntPtr.Zero)
                    throw new Exception("Failed to create Graphviz context.");

                // upd convert to utf-8, otherwise GraphViz displays Russian characters as krakozyabry
                Encoding utf8 = Encoding.GetEncoding("utf-8");
                Encoding win1251 = Encoding.GetEncoding("windows-1251");
                byte[] utf8Bytes = win1251.GetBytes(source);
                byte[] win1251Bytes = Encoding.Convert(win1251, utf8, utf8Bytes);
                source = win1251.GetString(win1251Bytes);


                // Load the DOT data into a graph 
                g = agmemread(source);
                if (g == IntPtr.Zero)
                    throw new Exception("Failed to create graph from source. Check for syntax errors.");

                // Apply a layout 
                if (gvLayout(gvc, g, layout) != SUCCESS)
                    throw new Exception("Layout failed.");

                IntPtr result;
                int length;

                // Render the graph 
                if (gvRenderData(gvc, g, format, out result, out length) != SUCCESS)
                    throw new Exception("Render failed.");

                // Create an array to hold the rendered graph
                byte[] bytes = new byte[length];
                // Copy the image from the IntPtr 
                Marshal.Copy(result, bytes, 0, length);
                return bytes;
            }
            finally
            {
                // Free up the resources 
                gvFreeLayout(gvc, g);
                agclose(g);
                gvFreeContext(gvc);
            }
        }

    }


    public static class ClsForInteractionWithGraphvizThroughProcess
    {
        //Image return
        public static byte[] CreateImage(string dot, string imagetype)
        {
            //the entire bin folder of graphviz project is needed
            string executable = @".\external\bin\dot.exe";
            string output = @"D:\tempgraph"; 
            File.WriteAllText(output, dot);

            System.Diagnostics.Process process = new System.Diagnostics.Process();

            // Stop the process from opening a new window (without show it)
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            // Setup executable and parameters
            process.StartInfo.FileName = executable;
            process.StartInfo.Arguments = string.Format(@"{0} -T{1} -O -Tps:cairo", output, imagetype);

            // Go
            process.Start();
            // and wait dot.exe to complete and exit
            process.WaitForExit();


            Image image;
            byte[] b;
            using (Stream bmpStream = System.IO.File.Open(output + "." + imagetype, System.IO.FileMode.Open))
            {
                //image = Image.FromStream(bmpStream);

                using (MemoryStream ms = new MemoryStream())
                {
                    bmpStream.CopyTo(ms);
                    b = ms.ToArray();
                }
            }
            //File.Delete(output);
            //File.Delete(output + "."+type);
            //return image;
            return b;
        }
    }

}