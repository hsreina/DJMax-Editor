using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DJMaxEditor
{
    class Logs
    {
        public delegate void AddWriteDelegate(string s);

        public static AddWriteDelegate OnLogWrite { get; set; }

        public static void Write(string content, params object[] args)
        {
            try
            {
#if DEBUG
                string str = String.Format(content, args);
                if (OnLogWrite != null)
                {
                    OnLogWrite(str);
                }
#endif
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception : {0}", e.Message);
            }
        }
    }
}
