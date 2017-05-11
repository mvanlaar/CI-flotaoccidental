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
using CsvHelper;

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

                        Boolean FlightMonday = false;
                        Boolean FlightTuesday = false;
                        Boolean FlightWednesday = false;
                        Boolean FlightThursday = false;
                        Boolean FlightFriday = false;
                        Boolean FlightSaterday = false;
                        Boolean FlightSunday = false;

                        int dayofweek = Convert.ToInt32(Date.DayOfWeek);
                        if (dayofweek == 0) { FlightSunday = true; }
                        if (dayofweek == 1) { FlightMonday = true; }
                        if (dayofweek == 2) { FlightTuesday = true; }
                        if (dayofweek == 3) { FlightWednesday = true; }
                        if (dayofweek == 4) { FlightThursday = true; }
                        if (dayofweek == 5) { FlightFriday = true; }
                        if (dayofweek == 6) { FlightSaterday = true; }


                        _Routes.Add(new CIBusRoutes { From = FromToCombo.Origen_Ciudad_Nombre,
                            To = FromToCombo.Destino_Ciudad_Nombre,
                            DepartTime = DepartTimeDT,
                            ArrivalTime = ArrivalTimeDT,
                            TypeVehicle = TypeVehicle,
                            FromDate = Date.Date,
                            ToDate = Date.Date,
                            FlightMonday = FlightMonday,
                            FlightTuesday = FlightTuesday,
                            FlightWednesday = FlightWednesday,
                            FlightThursday = FlightThursday,
                            FlightFriday = FlightFriday,
                            FlightSaterday = FlightSaterday,
                            FlightSunday = FlightSunday,
                            FlightNextDayArrival = false
                        });
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



            // GTFS Support

            string gtfsDir = AppDomain.CurrentDomain.BaseDirectory + "\\gtfs";
            System.IO.Directory.CreateDirectory(gtfsDir);

            Console.WriteLine("Creating GTFS Files...");

            Console.WriteLine("Creating GTFS File agency.txt...");
            using (var gtfsagency = new StreamWriter(@"gtfs\\agency.txt"))
            {
                var csv = new CsvWriter(gtfsagency);
                csv.Configuration.Delimiter = ",";
                csv.Configuration.Encoding = Encoding.UTF8;
                csv.Configuration.TrimFields = true;
                // header 
                csv.WriteField("agency_id");
                csv.WriteField("agency_name");
                csv.WriteField("agency_url");
                csv.WriteField("agency_timezone");
                csv.WriteField("agency_lang");
                csv.WriteField("agency_phone");
                csv.WriteField("agency_fare_url");
                csv.WriteField("agency_email");
                csv.NextRecord();

                csv.WriteField("FO");
                csv.WriteField("Flota Occidental");
                csv.WriteField("http://flotaoccidental.co/");
                csv.WriteField("America/Bogota");
                csv.WriteField("ES");
                csv.WriteField("+57 (6) 321-1655");
                csv.WriteField("");
                csv.WriteField("servicioalcliente@flotaoccidental.com");
                csv.NextRecord();
            }

            Console.WriteLine("Creating GTFS File routes.txt ...");

            using (var gtfsroutes = new StreamWriter(@"gtfs\\routes.txt"))
            {
                // Route record


                var csvroutes = new CsvWriter(gtfsroutes);
                csvroutes.Configuration.Delimiter = ",";
                csvroutes.Configuration.Encoding = Encoding.UTF8;
                csvroutes.Configuration.TrimFields = true;
                // header 
                csvroutes.WriteField("route_id");
                csvroutes.WriteField("agency_id");
                csvroutes.WriteField("route_short_name");
                csvroutes.WriteField("route_long_name");
                csvroutes.WriteField("route_desc");
                csvroutes.WriteField("route_type");
                csvroutes.WriteField("route_url");
                csvroutes.WriteField("route_color");
                csvroutes.WriteField("route_text_color");
                csvroutes.NextRecord();

                var routes = _Routes.Select(m => new { m.From, m.To }).Distinct().ToList();

                for (int i = 0; i < routes.Count; i++) // Loop through List with for)
                {
                    string FromAirportName = null;
                    string ToAirportName = null;
                    //using (var client = new WebClient())
                    //{
                    //    client.Encoding = Encoding.UTF8;
                    //    string urlapi = ConfigurationManager.AppSettings.Get("APIUrl") + APIPathAirport + routes[i].FromIATA;
                    //    var jsonapi = client.DownloadString(urlapi);
                    //    dynamic AirportResponseJson = JsonConvert.DeserializeObject(jsonapi);
                    //    FromAirportName = Convert.ToString(AirportResponseJson[0].name);
                    //}
                    //using (var client = new WebClient())
                    //{
                    //    client.Encoding = Encoding.UTF8;
                    //    string urlapi = ConfigurationManager.AppSettings.Get("APIUrl") + APIPathAirport + routes[i].ToIATA;
                    //    var jsonapi = client.DownloadString(urlapi);
                    //    dynamic AirportResponseJson = JsonConvert.DeserializeObject(jsonapi);
                    //    ToAirportName = Convert.ToString(AirportResponseJson[0].name);
                    //}

                    csvroutes.WriteField(routes[i].From + routes[i].To);
                    csvroutes.WriteField("Flota Occidental");
                    csvroutes.WriteField(routes[i].From + routes[i].To);
                    csvroutes.WriteField(routes[i].From + " - " + routes[i].To);
                    csvroutes.WriteField(""); // routes[i].FlightAircraft + ";" + _Routes[i].FlightAirline + ";" + _Routes[i].FlightOperator + ";" + _Routes[i].FlightCodeShare
                    csvroutes.WriteField(700);
                    csvroutes.WriteField("");
                    csvroutes.WriteField("");
                    csvroutes.WriteField("");
                    csvroutes.NextRecord();
                }
            }

            // stops.txt

            List<string> agencyairportsiata =
             _Routes.SelectMany(m => new string[] { m.From, m.To })
                     .Distinct()
                     .ToList();

            using (var gtfsstops = new StreamWriter(@"gtfs\\stops.txt"))
            {
                // Route record
                var csvstops = new CsvWriter(gtfsstops);
                csvstops.Configuration.Delimiter = ",";
                csvstops.Configuration.Encoding = Encoding.UTF8;
                csvstops.Configuration.TrimFields = true;
                // header                                 
                csvstops.WriteField("stop_id");
                csvstops.WriteField("stop_name");
                csvstops.WriteField("stop_desc");
                csvstops.WriteField("stop_lat");
                csvstops.WriteField("stop_lon");
                csvstops.WriteField("zone_id");
                csvstops.WriteField("stop_url");
                csvstops.WriteField("stop_timezone");
                csvstops.NextRecord();

                for (int i = 0; i < agencyairportsiata.Count; i++) // Loop through List with for)
                {
                    // Using API for airport Data.
                    //using (var client = new WebClient())
                    //{
                    //    client.Encoding = Encoding.UTF8;
                    //    string urlapi = ConfigurationManager.AppSettings.Get("APIUrl") + APIPathAirport + agencyairportsiata[i];
                    //    var jsonapi = client.DownloadString(urlapi);
                    //    dynamic AirportResponseJson = JsonConvert.DeserializeObject(jsonapi);

                    //csvstops.WriteField(Convert.ToString(AirportResponseJson[0].code));
                    //csvstops.WriteField(Convert.ToString(AirportResponseJson[0].name));
                    //csvstops.WriteField("");
                    //csvstops.WriteField(Convert.ToString(AirportResponseJson[0].lat));
                    //csvstops.WriteField(Convert.ToString(AirportResponseJson[0].lng));
                    //csvstops.WriteField("");
                    //csvstops.WriteField(Convert.ToString(AirportResponseJson[0].website));
                    //csvstops.WriteField(Convert.ToString(AirportResponseJson[0].timezone));
                    //csvstops.NextRecord();

                    csvstops.WriteField("FO-BUS-" + agencyairportsiata[i]);
                    csvstops.WriteField(agencyairportsiata[i]);
                    csvstops.WriteField("");
                    csvstops.WriteField("LAT");
                    csvstops.WriteField("LNG");
                    csvstops.WriteField("");
                    csvstops.WriteField("");
                    csvstops.WriteField("America/Bogota");
                    csvstops.NextRecord();
                    //}
                }
            }


            Console.WriteLine("Creating GTFS File trips.txt, stop_times.txt, calendar.txt ...");

            using (var gtfscalendar = new StreamWriter(@"gtfs\\calendar.txt"))
            {
                using (var gtfstrips = new StreamWriter(@"gtfs\\trips.txt"))
                {
                    using (var gtfsstoptimes = new StreamWriter(@"gtfs\\stop_times.txt"))
                    {
                        // Headers 
                        var csvstoptimes = new CsvWriter(gtfsstoptimes);
                        csvstoptimes.Configuration.Delimiter = ",";
                        csvstoptimes.Configuration.Encoding = Encoding.UTF8;
                        csvstoptimes.Configuration.TrimFields = true;
                        // header 
                        csvstoptimes.WriteField("trip_id");
                        csvstoptimes.WriteField("arrival_time");
                        csvstoptimes.WriteField("departure_time");
                        csvstoptimes.WriteField("stop_id");
                        csvstoptimes.WriteField("stop_sequence");
                        csvstoptimes.WriteField("stop_headsign");
                        csvstoptimes.WriteField("pickup_type");
                        csvstoptimes.WriteField("drop_off_type");
                        csvstoptimes.WriteField("shape_dist_traveled");
                        csvstoptimes.WriteField("timepoint");
                        csvstoptimes.NextRecord();

                        var csvtrips = new CsvWriter(gtfstrips);
                        csvtrips.Configuration.Delimiter = ",";
                        csvtrips.Configuration.Encoding = Encoding.UTF8;
                        csvtrips.Configuration.TrimFields = true;
                        // header 
                        csvtrips.WriteField("route_id");
                        csvtrips.WriteField("service_id");
                        csvtrips.WriteField("trip_id");
                        csvtrips.WriteField("trip_headsign");
                        csvtrips.WriteField("trip_short_name");
                        csvtrips.WriteField("direction_id");
                        csvtrips.WriteField("block_id");
                        csvtrips.WriteField("shape_id");
                        csvtrips.WriteField("wheelchair_accessible");
                        csvtrips.WriteField("bikes_allowed");
                        csvtrips.NextRecord();

                        var csvcalendar = new CsvWriter(gtfscalendar);
                        csvcalendar.Configuration.Delimiter = ",";
                        csvcalendar.Configuration.Encoding = Encoding.UTF8;
                        csvcalendar.Configuration.TrimFields = true;
                        // header 
                        csvcalendar.WriteField("service_id");
                        csvcalendar.WriteField("monday");
                        csvcalendar.WriteField("tuesday");
                        csvcalendar.WriteField("wednesday");
                        csvcalendar.WriteField("thursday");
                        csvcalendar.WriteField("friday");
                        csvcalendar.WriteField("saturday");
                        csvcalendar.WriteField("sunday");
                        csvcalendar.WriteField("start_date");
                        csvcalendar.WriteField("end_date");
                        csvcalendar.NextRecord();

                        //1101 International Air Service
                        //1102 Domestic Air Service
                        //1103 Intercontinental Air Service
                        //1104 Domestic Scheduled Air Service


                        for (int i = 0; i < _Routes.Count; i++) // Loop through List with for)
                        {

                            // Calender

                            csvcalendar.WriteField(_Routes[i].From + _Routes[i].To + String.Format("{0:yyyyMMdd}", _Routes[i].FromDate) + String.Format("{0:yyyyMMdd}", _Routes[i].ToDate));
                            csvcalendar.WriteField(Convert.ToInt32(_Routes[i].FlightMonday));
                            csvcalendar.WriteField(Convert.ToInt32(_Routes[i].FlightTuesday));
                            csvcalendar.WriteField(Convert.ToInt32(_Routes[i].FlightWednesday));
                            csvcalendar.WriteField(Convert.ToInt32(_Routes[i].FlightThursday));
                            csvcalendar.WriteField(Convert.ToInt32(_Routes[i].FlightFriday));
                            csvcalendar.WriteField(Convert.ToInt32(_Routes[i].FlightSaterday));
                            csvcalendar.WriteField(Convert.ToInt32(_Routes[i].FlightSunday));
                            csvcalendar.WriteField(String.Format("{0:yyyyMMdd}", _Routes[i].FromDate));
                            csvcalendar.WriteField(String.Format("{0:yyyyMMdd}", _Routes[i].ToDate));
                            csvcalendar.NextRecord();

                            // Trips
                            string FromAirportName = null;
                            string ToAirportName = null;
                            //using (var client = new WebClient())
                            //{
                            //    client.Encoding = Encoding.UTF8;
                            //    string urlapi = ConfigurationManager.AppSettings.Get("APIUrl") + APIPathAirport + _Routes[i].FromIATA;
                            //    var jsonapi = client.DownloadString(urlapi);
                            //    dynamic AirportResponseJson = JsonConvert.DeserializeObject(jsonapi);
                            //    FromAirportName = Convert.ToString(AirportResponseJson[0].name);
                            //}
                            //using (var client = new WebClient())
                            //{
                            //    client.Encoding = Encoding.UTF8;
                            //    string urlapi = ConfigurationManager.AppSettings.Get("APIUrl") + APIPathAirport + _Routes[i].ToIATA;
                            //    var jsonapi = client.DownloadString(urlapi);
                            //    dynamic AirportResponseJson = JsonConvert.DeserializeObject(jsonapi);
                            //    ToAirportName = Convert.ToString(AirportResponseJson[0].name);
                            //}


                            csvtrips.WriteField(_Routes[i].From + _Routes[i].To);
                            csvtrips.WriteField(_Routes[i].From + _Routes[i].To  + String.Format("{0:yyyyMMdd}", _Routes[i].FromDate) + String.Format("{0:yyyyMMdd}", _Routes[i].ToDate));
                            csvtrips.WriteField(_Routes[i].From + _Routes[i].To  + String.Format("{0:yyyyMMdd}", _Routes[i].FromDate) + String.Format("{0:yyyyMMdd}", _Routes[i].ToDate));
                            csvtrips.WriteField(_Routes[i].To);
                            csvtrips.WriteField(_Routes[i].From + _Routes[i].To);
                            csvtrips.WriteField("");
                            csvtrips.WriteField("");
                            csvtrips.WriteField("");
                            csvtrips.WriteField("1");
                            csvtrips.WriteField("");
                            csvtrips.NextRecord();

                            // Depart Record
                            csvstoptimes.WriteField(_Routes[i].From + _Routes[i].To + String.Format("{0:yyyyMMdd}", _Routes[i].FromDate) + String.Format("{0:yyyyMMdd}", _Routes[i].ToDate));
                            csvstoptimes.WriteField(String.Format("{0:HH:mm:ss}", _Routes[i].DepartTime));
                            csvstoptimes.WriteField(String.Format("{0:HH:mm:ss}", _Routes[i].DepartTime));
                            csvstoptimes.WriteField(_Routes[i].From);
                            csvstoptimes.WriteField("0");
                            csvstoptimes.WriteField("");
                            csvstoptimes.WriteField("0");
                            csvstoptimes.WriteField("0");
                            csvstoptimes.WriteField("");
                            csvstoptimes.WriteField("");
                            csvstoptimes.NextRecord();
                            // Arrival Record
                            if (!_Routes[i].FlightNextDayArrival)
                            {
                                csvstoptimes.WriteField(_Routes[i].From + _Routes[i].To + String.Format("{0:yyyyMMdd}", _Routes[i].FromDate) + String.Format("{0:yyyyMMdd}", _Routes[i].ToDate));
                                csvstoptimes.WriteField(String.Format("{0:HH:mm:ss}", _Routes[i].ArrivalTime));
                                csvstoptimes.WriteField(String.Format("{0:HH:mm:ss}", _Routes[i].ArrivalTime));
                                csvstoptimes.WriteField(_Routes[i].To);
                                csvstoptimes.WriteField("2");
                                csvstoptimes.WriteField("");
                                csvstoptimes.WriteField("0");
                                csvstoptimes.WriteField("0");
                                csvstoptimes.WriteField("");
                                csvstoptimes.WriteField("");
                                csvstoptimes.NextRecord();
                            }
                            else
                            {
                                //add 24 hour for the gtfs time
                                int hour = _Routes[i].ArrivalTime.Hour;
                                hour = hour + 24;
                                int minute = _Routes[i].ArrivalTime.Minute;
                                string strminute = minute.ToString();
                                if (strminute.Length == 1) { strminute = "0" + strminute; }
                                csvstoptimes.WriteField(_Routes[i].From + _Routes[i].To + String.Format("{0:yyyyMMdd}", _Routes[i].FromDate) + String.Format("{0:yyyyMMdd}", _Routes[i].ToDate));
                                csvstoptimes.WriteField(hour + ":" + strminute + ":00");
                                csvstoptimes.WriteField(hour + ":" + strminute + ":00");
                                csvstoptimes.WriteField(_Routes[i].To);
                                csvstoptimes.WriteField("2");
                                csvstoptimes.WriteField("");
                                csvstoptimes.WriteField("0");
                                csvstoptimes.WriteField("0");
                                csvstoptimes.WriteField("");
                                csvstoptimes.WriteField("");
                                csvstoptimes.NextRecord();
                            }
                        }
                    }
                }
            }

            // Create Zip File
            string startPath = gtfsDir;
            string zipPath = myDir + "\\BUS-FlotaOccidental.zip";
            if (File.Exists(zipPath)) { File.Delete(zipPath); }
            ZipFile.CreateFromDirectory(startPath, zipPath, CompressionLevel.Fastest, false);


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
            public DateTime FromDate;
            public DateTime ToDate;
            public Boolean FlightMonday;
            public Boolean FlightTuesday;
            public Boolean FlightWednesday;
            public Boolean FlightThursday;
            public Boolean FlightFriday;
            public Boolean FlightSaterday;
            public Boolean FlightSunday;
            public Boolean FlightNextDayArrival;

        }
    }
}
