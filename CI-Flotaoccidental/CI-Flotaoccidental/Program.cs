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
                postData += String.Format("&fecha=2017-05-11");
                //postData += String.Format("&title=Viajes+de+Ida&seleccion=Ida&lang=spanish");                
                var data = Encoding.ASCII.GetBytes(postData);

                request.Method = "POST";
                request.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
                request.ContentLength = data.Length;
                request.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64; Trident/7.0; rv:11.0) like Gecko";
                request.Referer = "http://flotaoccidental.co/horarios";
                request.Headers.Add("X-Requested-With", "XMLHttpRequest");

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
                    HtmlNode InputNode = RouteTime.SelectSingleNode("./input");
                    string DepartTime = RouteTime.SelectSingleNode("/table[1]/tbody[1]/tr[1]/td[3]").InnerText.ToString();
                    string RitTime = RouteTime.SelectSingleNode("/table[1]/tbody[1]/tr[1]/td[4]").InnerText.ToString();
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
            public string Origen_Ciudad_Nombre;
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
