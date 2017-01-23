using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
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

            // http://flotaoccidental.co/horarios is better source, but contains no route information.
            List<CIBusOrigens> _Origens = new List<CIBusOrigens> { };
            List<CIBusOrigensDestino> _OrigensDestino = new List<CIBusOrigensDestino> { };
            List<CIBusTramoSteps> _TramoSteps = new List<CIBusTramoSteps> { };
            List<CIBusRoutes> _Routes = new List<CIBusRoutes> { };
            List<CIBusRoutesDetails> _RoutesDetails = new List<CIBusRoutesDetails> { };

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
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(OrigensHtml);
            var nodes = doc.DocumentNode.SelectNodes("//select[@id='origenCompra']/option");
            foreach (var node in nodes)
            {
                string AirportName = node.NextSibling.InnerText;
                string AirportValue = node.Attributes["value"].Value;                
                AirportName = AirportName.Trim();
                AirportValue = AirportValue.Trim();
                if (AirportValue != "0")
                {
                    _Origens.Add(new CIBusOrigens { Ciudad_ID = AirportValue, Ciudad_Nombre = AirportName });
                }
            }

            Console.WriteLine("Parsing through the from to get the destionations for each from locations...");
            foreach (var Origen in _Origens) 
            {
                var request = (HttpWebRequest)WebRequest.Create("http://www.flotaoccidental.com/formprueba/destinos.php");

                var postData = String.Format("destino={0}", Origen.Ciudad_ID);
                var data = Encoding.ASCII.GetBytes(postData);

                request.Method = "POST";
                request.ContentType = "application/x-www-form-urlencoded";
                request.ContentLength = data.Length;
                request.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64; Trident/7.0; rv:11.0) like Gecko";

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
                    string Destino_CIUDAD_ID = destino.coddes;
                    string Destino_CIUDAD_NOMBRE = destino.descripcion;
                    if (Destino_CIUDAD_ID != "0")
                    {
                        _OrigensDestino.Add(new CIBusOrigensDestino { Origen_Ciudad_ID = Origen.Ciudad_ID, Origen_Ciudad_Nombre = Origen.Ciudad_Nombre, Destino_Ciudad_ID = Destino_CIUDAD_ID, Destino_Ciudad_Nombre = Destino_CIUDAD_NOMBRE });
                    }
                }
                // Response JSON: [{"coddes":"401","descripcion":"Medell\u00edn"},{"coddes":"503","descripcion":"Condoto"},{"coddes":"506","descripcion":"Istmina"},{"coddes":"501","descripcion":"Quibdo"},{"coddes":"508","descripcion":"Tado"}]
            }
            // Begin parsing route information
            foreach (var FromToCombo in _OrigensDestino)
            {
                var request = (HttpWebRequest)WebRequest.Create("http://www.flotaoccidental.com/carrito/cargarViajes");
                //fecha=2017%2F01%2F27&origen=101&destino=401&title=Viajes+de+Ida&seleccion=Ida&lang=spanish
                var postData = String.Format("fecha=2017%2F01%2F27");
                postData += String.Format("&origen={0}", FromToCombo.Origen_Ciudad_ID);
                postData += String.Format("&destino={0}", FromToCombo.Destino_Ciudad_ID);
                postData += String.Format("&title=Viajes+de+Ida&seleccion=Ida&lang=spanish");                
                var data = Encoding.ASCII.GetBytes(postData);

                request.Method = "POST";
                request.ContentType = "application/x-www-form-urlencoded";
                request.ContentLength = data.Length;
                request.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64; Trident/7.0; rv:11.0) like Gecko";

                using (var stream = request.GetRequestStream())
                {
                    stream.Write(data, 0, data.Length);
                }
                var response = (HttpWebResponse)request.GetResponse();
                var responseString = new StreamReader(response.GetResponseStream()).ReadToEnd();
                // Reponse is html.
                HtmlDocument RouteTimesHtml = new HtmlDocument();
                RouteTimesHtml.LoadHtml(responseString);
                var RouteTimes = doc.DocumentNode.SelectNodes("//table//tbody//tr");
                foreach (var RouteTime in RouteTimes)
                {
                    // Select Input
                    HtmlNode InputNode = RouteTime.SelectSingleNode("./input");
                    string DepartTime = InputNode.Attributes["data-fechatiq"].Value;

                }
            }
        }

        [Serializable]
        public class CIBusOrigens
        {
            // Auto-implemented properties. 

            public string Ciudad_ID;
            public string Ciudad_Nombre;
        }
        [Serializable]
        public class CIBusOrigensDestino
        {
            // Auto-implemented properties. 

            public string Origen_Ciudad_ID;
            public string Origen_Ciudad_Nombre;
            public string Destino_Ciudad_ID;
            public string Destino_Ciudad_Nombre;
        }

        [Serializable]
        public class CIBusTramo
        {
            // Auto-implemented properties. 

            public string Origen_Ciudad_ID;
            public string Origen_Ciudad_Nombre;
            public string Destino_Ciudad_ID;
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

            public string RutaNr;
            public string From;
            public string To;

        }
        [Serializable]
        public class CIBusRoutesDetails
        {
            // Auto-implemented properties. 

            public string EMPRESA;
            public string EMPRESAN;
            public string AGENCIA;
            public string AGENCIAN;
            public string CIUDADN;
            public string DEPARTAMENTON;
            public string PAISN;
            public string RUTA;
            public string KILOMETROS;
            public string MINUTOS;
        }

    }
}
