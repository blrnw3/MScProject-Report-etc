﻿
using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using CommandLine;

namespace GadgeteerSimulator {
	class Program {

		static Random r = new Random();

		/// <summary>
		/// Sensor Channels - one for each measurement property
		/// </summary>
		private static readonly string[] channelNames = {
				"temp1", "humi", "motion", "light", "temp2", "mass", "tempdiff" };

		/**
		 * Source of HTTP Send and CreateRequest methods: MSDN C# documentation
		 */
		/**
		 * Source of Command-line arg parsing: http://commandline.codeplex.com/
		 * 
		*/

		const string APIkey = "blr2013ucl";

		static string RESTurlPublic = "http://hivesensenodejs.azurewebsites.net";
		static string RESTurlLocal = "http://localhost:1337";
		static string RESTurl;

		static bool bigRandom;

		public static void Send( byte[] sample, string url ) {
			Console.Out.WriteLine( "time: " + DateTime.Now );
			try {
				var request = CreateRequest( sample, url );
				request.Timeout = 5000; // 5 seconds
				// send request and receive response
				using(var response =
				(HttpWebResponse) request.GetResponse()) {
					HandleResponse( response );
				}
			} catch(Exception e) {
				Console.Out.WriteLine( e.ToString() );
			}
		}

		static HttpWebRequest CreateRequest( byte[] sample, string RESTurl ) {
		
			var request = (HttpWebRequest) WebRequest.Create( RESTurl );

			request.Method = "PUT";
			//Headers
			request.UserAgent = "HiveSenseV1 - Gadgeteer UCL";
			request.ContentLength = sample.Length;
			request.ContentType = "application/json";
			request.Headers.Add( "X-hiveSenseSecurekey", APIkey );

			//Body
			using(System.IO.Stream stream = request.GetRequestStream()) {
				stream.Write( sample, 0, sample.Length );
			}

			return request;
		}

		public static void HandleResponse( HttpWebResponse response ) {
			Console.Out.WriteLine( "Status code: " + response.StatusCode );
			Stream dataStream = response.GetResponseStream();
			// Open the stream using a StreamReader for easy access.
			StreamReader reader = new StreamReader( dataStream );
			// Read the content.
			string responseFromServer = reader.ReadToEnd();
			// Display the content.
			//Console.WriteLine( "response: " + responseFromServer );
			// Clean up the streams.
			reader.Close();
			dataStream.Close();
			response.Close();
		}

		static string randomSensorValue( int minInt, int maxInt, int precision ) {
			double timestamp = System.DateTime.UtcNow.Hour * 60 + System.DateTime.UtcNow.Minute;
			double baseValue = minInt + (maxInt - minInt) / 1440.0 * timestamp;
			//Console.Out.WriteLine( baseValue );
			//Increase by small randomly-decided fraction and clean up.
			int smallness = bigRandom ? 8 : 25;
			return Math.Round( baseValue * (r.NextDouble() / smallness + 1), precision ).ToString();
		}

		static string[] getRandomDatapoint() {
			var fargs = new string[channelNames.Length];
			fargs[0] = randomSensorValue( 20, 42, 1 );
			fargs[1] = randomSensorValue( 30, 90, 0 );
			fargs[2] = Math.Round( r.NextDouble() - 0.45 ).ToString(); //random boolean, biased towards false
			fargs[3] = randomSensorValue( 0, 50, 0 );
			fargs[4] = randomSensorValue( 9, 33, 1 );
			fargs[5] = randomSensorValue( 89, 99, 1 );
			fargs[6] = (Double.Parse( fargs[0] ) - Double.Parse( fargs[4] )).ToString();

			return fargs;
		}

		static void sendTextyPost(string text) {

			if(text.Length > 10) {
				string datastream = jsonWrap( text );

				Send( Encoding.UTF8.GetBytes( datastream ), RESTurl );
			} else {
				Console.Out.WriteLine( "No text data to send" );
			}
		}

		static void compileSingleDataPoint() {
			sendTextyPost( dataLineToAPIFormat( getRandomDatapoint(), "" ) );
		}

		static void compileMultipleDatapoints() {
			sendTextyPost( getHistoricalData( @"..\..\datalog.csv" ) );
		}

		public static byte[] getImageBytesForPOST() {
			var pic = Image.FromFile( @"C:\Users\Ben LR\Downloads\test.jpg" );
			ImageConverter converter = new ImageConverter();
			return (byte[]) converter.ConvertTo( pic, typeof( byte[] ) );
		}

		/// <summary>
		/// Get data into API-compatible JSON format<br />
		/// No native JSON builder exists for NETMF, so this very ugly and specific fn is necessary
		/// </summary>
		/// <param name="data">raw, but clean, data</param>
		/// <param name="timestamp">timestamp of the data - use empty string to ignore this</param>
		/// <returns>compatible compiled data</returns>
		static string dataLineToAPIFormat( string[] data, string timestamp ) {
			string compiledData = "{\"channels\":{";

			for(int i = 0; i < channelNames.Length; i++) {
				//	time = useTimestamp ? 
				compiledData += "\"" + channelNames[i] + "\":\"" + data[i] + "\"";
				if(i < channelNames.Length - 1) {
					compiledData += ",";
				}
			}

			compiledData += "}";
			if(timestamp.Length > 0) {
				compiledData += ",\"datetime\":\"" + timestamp + "\"";
			}
			compiledData += "}";

			return compiledData;
		}

		static string jsonWrap( string jsonDatapoints ) {
			string full = "{\"datapoints\":[" + jsonDatapoints + "]}";
			Console.Out.WriteLine( "compiled as " + full );
			return full;
		}


		/// <summary>
		/// PUTs buffered csv data to the API
		/// </summary>
		/// <param name="path">path to log</param>
		/// <returns><c>true</c> on success (complete transmission), <c>false</c> on failure</returns>
		public static string getHistoricalData( string path ) {
			var compiledData = new System.Text.StringBuilder();
			string[] data = new string[channelNames.Length];
			string[] fields;
			string timestamp;
			int validCnt = 0;

			foreach(string dataLine in File.ReadLines( path )) {
	
				if(dataLine != null && dataLine.Length > 10) {
					fields = dataLine.Split( ',' );
					for(int i = 0; i <= 5; i++) {
						fields[i] = zerolead( fields[i] );
					}

					timestamp = fields[0] + '-' + fields[1] + '-' + fields[2] + 'T' +
						fields[3] + ':' + fields[4] + ':' + fields[5] + "Z";

					for(int i = 0; i < data.Length; i++) {
						data[i] = fields[i + 6];
					}
					if(fields[0] != "1970") { //default year when not time-synced
						compiledData.Append( dataLineToAPIFormat( data, timestamp ) + "," );
						validCnt++;
					}
				}
			}
			return compiledData.ToString().Substring(0, compiledData.Length-1);
		}

		private static string zerolead( string i ) {
			int num = Int32.Parse( i );
			return (num < 10) ? '0' + num.ToString() : num.ToString();
		}

		private static void saveDataPoints(Options o) {
			string[] date = o.date.Split( '/' );
			var start = new DateTime( Int32.Parse(date[0]), Int32.Parse(date[1]), Int32.Parse(date[2]) );

			int[] dates = new int[6];
			string[] rdp;
			int cnt = 0;

			while(cnt < o.number) {
				string line = "";
				rdp = getRandomDatapoint();

				dates[0] = start.Year;
				dates[1] = start.Month;
				dates[2] = start.Day;
				dates[3] = start.Hour;
				dates[4] = start.Minute;
				dates[5] = start.Second;

				for(int i = 0; i < dates.Length; i++) {
					line += dates[i] + ",";
				}

				for(int i = 0; i < rdp.Length; i++) {
					line += rdp[i];
					if(i < rdp.Length - 1) {
						line += ",";
					}
				}

				start = start.AddSeconds( o.interval );
				cnt++;

				Console.Out.WriteLine( line );
			}
		}


		static void Main( string[] args ) {

			var options = new Options();
			if(CommandLine.Parser.Default.ParseArguments( args, options )) {

				RESTurl = options.isLocal ? RESTurlLocal : RESTurlPublic;

				if(options.isImage) {
					RESTurl += "/image";
					Send( getImageBytesForPOST(), RESTurl );
				}
				else if(options.save) {
					bigRandom = true;
					saveDataPoints(options);
				}
				else {
					RESTurl += "/feed";
					bigRandom = false;

					if(options.isHistory) {
						compileMultipleDatapoints();
					}
					else {
						int wait = options.interval;
						int limit = options.number;
						int cnt = 1;

						Console.Out.WriteLine( "Beginning hivesense Gadgeteer simulation.\n" +
							"System will auto-post a total of " + limit + " random data points at " + wait + "s intervals" );

						while(cnt <= limit) {
							Console.Out.WriteLine( "Posting random data point " + cnt + " of " + limit );
							compileSingleDataPoint();
							if(cnt < limit) {
								Console.Out.WriteLine( "waiting " + wait + "s before next post" );
								System.Threading.Thread.Sleep( wait * 1000 );
							}
							cnt++;
						}		
						Console.Out.WriteLine( "All posts sent" );	
					}

				}
			} else {
				Console.Out.WriteLine( "WTF?" );
			}

		}

	}


	class Options {
		[Option( 'l', "local", HelpText = "Use localhost rather than WWW" )]
		public bool isLocal { get; set; }

		[Option( 'i', "interval", DefaultValue = 60, HelpText = "Frequency of transmission." )]
		public int interval { get; set; }

		[Option( 'n', "number", DefaultValue = 60, HelpText = "Number of points to transmit." )]
		public int number { get; set; }

		[Option( 'h', "history", HelpText = "Load from historical log rather than do live sim." )]
		public bool isHistory { get; set; }

		[Option( 'b', "image", HelpText = "Send an image." )]
		public bool isImage { get; set; }

		[Option( 's', "save", HelpText = "Save data points to text file rather than transmit them." )]
		public bool save { get; set; }

		[Option( 'd', "date", HelpText = "Start date for saving data points" )]
		public string date { get; set; }

		[HelpOption]
		public string GetUsage() {
			// this without using CommandLine.Text
			//  or using HelpText.AutoBuild
			var usage = new StringBuilder();
			usage.AppendLine( "Quickstart Application 1.0" );
			usage.AppendLine( "Read user manual for usage instructions..." );
			return usage.ToString();
		}
	}

}