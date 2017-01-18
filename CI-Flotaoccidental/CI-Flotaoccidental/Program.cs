using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace CI_Flotaoccidental
{
    class Program
    {
        static void Main(string[] args)
        {
            String OrigensHtml = String.Empty;
            using (System.Net.WebClient wc = new WebClient())
            {
                
                wc.Headers.Add("user-agent", "Mozilla/5.0 (Windows NT 6.1; WOW64; Trident/7.0; rv:11.0) like Gecko");
                wc.Headers.Add("Referer", "http://www.flotaoccidental.com/");
                wc.Proxy = null;
                Console.WriteLine("Download Origens list");
                OrigensHtml = wc.DownloadString("http://www.flotaoccidental.com/formprueba/menu.php");
                Console.WriteLine("Download ready...");
            }

        }
    }
}
