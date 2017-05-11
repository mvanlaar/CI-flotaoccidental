using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace CI_Flotaoccidental
{
    public class Program
    {
        static void Main(string[] args)
        {

            // http://flotaoccidental.co/horarios is better source, but contains no route information.
            List<CIBusOrigens> _Origens = new List<CIBusOrigens> { };
            List<CIBusOrigensDestino> _OrigensDestino = new List<CIBusOrigensDestino> { };            
            List<CIBusRoutes> _Routes = new List<CIBusRoutes> { };
            DateTime Date = DateTime.Now;

            CookieContainer cookieContainer = new CookieContainer();
            CookieCollection cookieCollection = new CookieCollection();


            String OrigensHtml = String.Empty;
            using (System.Net.WebClient wc = new WebClient())
            {
                
                wc.Headers.Add("user-agent", "Mozilla/5.0 (Windows NT 6.1; WOW64; Trident/7.0; rv:11.0) like Gecko");
                wc.Headers.Add("Referer", "http://flotaoccidental.co/transporte-de-pasajeros/");
                wc.Proxy = null;
                Console.WriteLine("Download Origens list");
                OrigensHtml = wc.DownloadString("http://flotaoccidental.co/horarios");
                Console.WriteLine("Download ready...");
            }
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(OrigensHtml);
            var origens = doc.DocumentNode.SelectNodes("//select[@id='origen']/option");
            foreach (var origen in origens)
            {
                string OrigenName = origen.NextSibling.InnerText;
                OrigenName = OrigenName.Trim();
                _Origens.Add(new CIBusOrigens { Ciudad_Nombre = OrigenName });               
            }
            Console.WriteLine("Parsing through the from to get the destionations for each from locations...");
            foreach (var Origen in _Origens) 
            {
                var request = (HttpWebRequest)WebRequest.Create("http://flotaoccidental.co/horarios/destinos");

                var postData = String.Format("origen={0}", Origen.Ciudad_Nombre);
                var data = Encoding.ASCII.GetBytes(postData);

                request.Method = "POST";
                request.ContentType = "application/x-www-form-urlencoded";
                request.ContentLength = data.Length;
                request.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64; Trident/7.0; rv:11.0) like Gecko";
                request.Referer = "http://flotaoccidental.co/horarios";
                request.Headers.Add("X-Requested-With", "XMLHttpRequest");
                request.CookieContainer = cookieContainer;

                using (var stream = request.GetRequestStream())
                {
                    stream.Write(data, 0, data.Length);
                }
                var response = (HttpWebResponse)request.GetResponse();
                var responseString = new StreamReader(response.GetResponseStream()).ReadToEnd();
                
                // Parse the Response.
                dynamic DestinationResponseJson = JArray.Parse(responseString);
                foreach (var destino in DestinationResponseJson)
                {                   
                    string Destino_CIUDAD_NOMBRE = destino.destino;
                    _OrigensDestino.Add(new CIBusOrigensDestino { Origen_Ciudad_Nombre = Origen.Ciudad_Nombre, Destino_Ciudad_Nombre = Destino_CIUDAD_NOMBRE });                    
                }
                // Response JSON: [{"coddes":"401","descripcion":"Medell\u00edn"},{"coddes":"503","descripcion":"Condoto"},{"coddes":"506","descripcion":"Istmina"},{"coddes":"501","descripcion":"Quibdo"},{"coddes":"508","descripcion":"Tado"}]
            }
            // Begin parsing route information
            Console.WriteLine("Found: {0} combinations", _OrigensDestino.Count.ToString());
            foreach (var FromToCombo in _OrigensDestino)
            {
                var request = (HttpWebRequest)WebRequest.Create("http://flotaoccidental.co/horarios/consultaHorarios");
                //fecha=2017%2F01%2F27&origen=101&destino=401&title=Viajes+de+Ida&seleccion=Ida&lang=spanish
                var postData = String.Format("origen={0}", FromToCombo.Origen_Ciudad_Nombre);
                postData += String.Format("&destino={0}", FromToCombo.Destino_Ciudad_Nombre);
                postData += String.Format("&fecha={0}", Date.ToString("yyyy-MM-dd"));
                //postData += String.Format("&title=Viajes+de+Ida&seleccion=Ida&lang=spanish");                
                var data = Encoding.ASCII.GetBytes(postData);

                request.Method = "POST";
                request.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
                request.ContentLength = data.Length;
                request.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64; Trident/7.0; rv:11.0) like Gecko";
                request.Referer = "http://flotaoccidental.co/horarios";
                request.Headers.Add("X-Requested-With", "XMLHttpRequest");
                request.Accept = "gzip,deflate";
                request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                request.CookieContainer = cookieContainer;
                
                using (var stream = request.GetRequestStream())
                {
                    stream.Write(data, 0, data.Length);
                }
                var response = (HttpWebResponse)request.GetResponse();
                var responseString = new StreamReader(response.GetResponseStream()).ReadToEnd();
                // Reponse is html.
                HtmlDocument RouteTimesHtml = new HtmlDocument();
                RouteTimesHtml.LoadHtml(responseString);
                var RouteTimes = RouteTimesHtml.DocumentNode.SelectNodes("//table//tbody//tr");
                if (RouteTimes != null)
                {
                    foreach (var RouteTime in RouteTimes)
                    {
                        /* 
                         * <tr>
                         * <td>CORRIENTE</td>
                         * <td>2017-05-10</td> datum
                         * <td>9:00 am</td> vertrektijd
                         * <td>00:40:00</td> duur rit.
                         * <td>28</td> 
                         * 	</tr>
                         */                        
                        string DepartTime = RouteTime.SelectSingleNode("./td[3]").InnerText.ToString();
                        DateTime DepartTimeDT = DateTime.Parse(DepartTime);
                        string Duration = RouteTime.SelectSingleNode("./td[4]").InnerText.ToString();
                        TimeSpan DurationTS = TimeSpan.Parse(Duration);
                        DateTime ArrivalTimeDT = DepartTimeDT.Add(DurationTS);
                        string TypeVehicle = RouteTime.SelectSingleNode("./td[1]").InnerText.ToString();
                        _Routes.Add(new CIBusRoutes { From = FromToCombo.Origen_Ciudad_Nombre, To = FromToCombo.Destino_Ciudad_Nombre, DepartTime = DepartTimeDT, ArrivalTime = ArrivalTimeDT, TypeVehicle = TypeVehicle });
                    }
                }
            }

            // Export XML
            // Write the list of objects to a file.
            System.Xml.Serialization.XmlSerializer writer =
            new System.Xml.Serialization.XmlSerializer(_Routes.GetType());
            string myDir = AppDomain.CurrentDomain.BaseDirectory + "\\output";
            Directory.CreateDirectory(myDir);
            StreamWriter file =
               new System.IO.StreamWriter("output\\output.xml");

            writer.Serialize(file, _Routes);
            file.Close();
        }

        [Serializable]
        public class CIBusOrigens
        {
            // Auto-implemented properties.             
            public string Ciudad_Nombre;
        }
        [Serializable]
        public class CIBusOrigensDestino
        {
            // Auto-implemented properties.                         
            public string Origen_Ciudad_Nombre;
            public string Destino_Ciudad_Nombre;
        }

        [Serializable]
        public class CIBusTramoSteps
        {
            // Auto-implemented properties. 

            public string RutaNr;
            public int Steps;

        }
        [Serializable]
        public class CIBusRoutes
        {
            // Auto-implemented properties. 
            public string From;
            public string To;
            public DateTime DepartTime;
            public DateTime ArrivalTime;
            public string TypeVehicle;

        }
    }
}
