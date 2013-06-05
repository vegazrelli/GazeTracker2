﻿// WSCom.WSClient.cs
// ------------------------------------------------------------------------
// This program is free software; you can redistribute it and/or modify it 
// under the terms of the GNU General Public License as published by the 
// Free Software Foundation; either version 3 of the License, 
// or (at your option) any later version.
// This program is distributed in the hope that it will be useful, 
// but WITHOUT ANY WARRANTY; without even the implied warranty of 
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
// General Public License for more details.
// You should have received a copy of the GNU General Public License 
// along with this program; if not, see http://www.gnu.org/licenses/.
// **************************************************************
// </copyright>
// <author>Nicola Riparelli</author>
// <email>nicola.riparelli@gmail.com</email>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using Alchemy;
using Alchemy.Classes;
using GazeTrackerClient;
using GTCommons.Events;
using Newtonsoft.Json;
using System.Threading;

namespace WSCom
{
    ///<summary>
    /// Class WSClient: it handles the connection between Eye tracker and the main software through TCP/UDP and WebSocket connections
    /// </summary>
    public class WSClient
    {
        #region VARIABLES

        WebSocketClient wsClient;
        static GazeTrackerClient.Client gtClient;

        private static int IDSession = 0;
        private static bool IDBoolSession = false;
        private static DateTime StartTime;
        private static TimeSpan GazeTime;
        private bool connected = false;
        private string logInfo;
        private static UserContext cont;
        private int screenWidth = 0;
        private int screenHeight = 0;
        private static int receivedWidth = 0;
        private static int receivedHeight = 0;
        private static bool onStream = false;
        private double gazeX = 0;
        private double gazeY = 0;


        #endregion

        #region EVENTS

        public ErrorEventArgs e = null;
        public delegate void ErrorEventHandler(object sender, ErrorEventArgs e);
        public event ErrorEventHandler Error;

        public delegate void ConnectionChangedHandler(object sender, StringEventArgs e);
        public event ConnectionChangedHandler ConnectionChange;
        
        #endregion

        #region CONSTRUCTOR

        ///<summary>
        /// Class Constructor that handles the Gazetracker and WebSocket clients.
        /// </summary>
        public WSClient()
        {
            createXml();
            wsClient = new WebSocketClient("ws://ciman.math.unipd.it:8000/")
            {
                OnConnected = OnConnected,
                OnDisconnect = OnDisconnect,
                OnFailedConnection = OnFailedConnection,
                OnReceive = OnReceive
            };
            gtClient = new GazeTrackerClient.Client();

            gtClient.ErrorOccured += OnErrorOccured;

            gtClient.Calibration.OnPointChange += new GazeTrackerClient.Calibration.PointChangeHandler(OnCalibrationPointChange);
            gtClient.Calibration.OnEnd += new GazeTrackerClient.Calibration.EndHandler(OnCalibrationEnd);
            gtClient.Calibration.OnQuality += new GazeTrackerClient.Calibration.QualityHandler(OnCalibrationQuality);
            gtClient.GazeData.OnGazeData += new GazeTrackerClient.GazeData.GazeDataHandler(OnGazeData);
        }

        ///<summary>
        /// Class Constructor that handles the Gazetracker and WebSocket clients.
        /// </summary>
        /// <param name="address">The server address: ws://address:port/</param>
        public WSClient(string address)
        {
            createXml();
            wsClient = new WebSocketClient(address)
            {
                OnConnected = OnConnected,
                OnDisconnect = OnDisconnect,
                OnFailedConnection = OnFailedConnection,
                OnReceive = OnReceive
            };
            gtClient = new GazeTrackerClient.Client();
        }

        #endregion

        #region PRIVATE METHODS
        
        // GazeTrackerClient events
        private void OnErrorOccured(object sender, StringEventArgs e)
        {
            var message = e.Param;
            Exception ex = new Exception(message);
            NotifyError(ex);
        }

        private void OnCalibrationPointChange(string e)
        {
            char[] seperator = { ' ' };
            string[] coordinates = e.Split(seperator, 3);
            string parameter1 = coordinates[0];
            string parameter2 = coordinates[1];
            string parameter3 = coordinates[2];

            char[] seperator2 = { ':' };

            string[] pointnumber = parameter1.Split(seperator2, 2);
            int finalPointNumber = Int32.Parse(pointnumber[1]);

            string[] coordinatex = parameter2.Split(seperator2, 2);
            int finalPointX = Int32.Parse(coordinatex[1]);

            string[] coordinatey = parameter3.Split(seperator2, 2);
            int finalPointY = Int32.Parse(coordinatey[1]);

            string message = finalPointNumber + " " + finalPointX + " " + finalPointY;
            SendMessage("CAL_POINT", message);
        }

        private void OnCalibrationEnd(int e)
        {
            SendMessage("CAL_END","");
        }

        private void OnCalibrationQuality(int e)
        {
            SendMessage("CAL_QUAL", " " + e);
        }

        private void OnCalibrationResult(int e)
        {
            SendMessage("CAL_RESULT", " " + e);
        }

        private void OnGazeData(GazeData data)
        {
            if (onStream)
            {
                //GazeTime = new TimeSpan();
                //GazeTime = DateTime.UtcNow - StartTime;
                TimeSpan GazeTimeLong = DateTime.UtcNow - StartTime;
                gazeX = Math.Round(data.GazePositionX, 0);
                gazeY = Math.Round(data.GazePositionY, 0);
                SendMessageData("GAZE_DATA", (long)GazeTimeLong.TotalMilliseconds, gazeX + " " + gazeY);
            }
        }
        // End

        // WebSocketClient events
        private void OnFailedConnection(UserContext context)
        {
            string errorMessage = "Connessione al software fallita";
            Exception ex = new Exception(errorMessage);
            NotifyError(ex);
        }

        private void OnConnected(UserContext context)
        {
            connected = true;
            StringEventArgs message = new StringEventArgs(connected.ToString());
            ConnectionChange(this, message);
        }

        private void OnDisconnect(UserContext context)
        {
            connected = false;
            StringEventArgs message = new StringEventArgs(connected.ToString());
            ConnectionChange(this, message);
        }

        ///<summary>
        /// Event fired when a data is received from the Alchemy Websockets server instance.
        /// Parses data as JSON and calls the appropriate message or sends an error message.
        /// </summary>
        /// <param name="context">The user's connection context</param>
        public static void OnReceive(UserContext context)
        {
            try
            {
                cont = context;
                var json = context.DataFrame.ToString();
                Console.WriteLine(json);
                dynamic obj = JsonConvert.DeserializeObject(json);
                Console.WriteLine(obj.TYPE);
                if ((obj.TYPE) == ("IDENTIFICATION"))
                {
                    string name = "EyeTrackerClient";
                    var r = new Response { TYPE = "IDENTIFICATION", DATA = name };
                    context.Send(JsonConvert.SerializeObject(r));
                }

                if ((obj.TYPE) == ("IDENTIFICATION_COMPLETE"))
                {
                    // devo recuperare ID salvato su file ini per esempio!
                    // se è vuoto restituisco stringa vuota
                    // se c'è invece restituisco stringa con id: TYPE: MACHINE_ID, DATA: valore

                    XmlReader reader = XmlReader.Create("Settings.xml");
                    string ID = "";
                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element && reader.Name == "EyeTracker")
                        {
                            ID = reader.GetAttribute(0);
                        }
                    }
                    if (ID.Equals(""))
                    {
                        var r = new Response { TYPE = "MACHINE_ID", DATA = "" };
                        context.Send(JsonConvert.SerializeObject(r));
                    }
                    else
                    {
                        var r = new Response { TYPE = "MACHINE_ID", DATA = ID };
                        context.Send(JsonConvert.SerializeObject(r));
                    }
                }

                if ((obj.TYPE) == ("OFFSET_CALCULATION"))
                {
                    if ((obj.TODO) == ("true"))
                    {
                        var r = new Response { TYPE = "START_OFFSET_CALCULATION", DATA = "" };
                        context.Send(JsonConvert.SerializeObject(r));
                    }
                    else
                    {
                        var r = new Response { TYPE = "READY_TO_PLAY", DATA = "" };
                        context.Send(JsonConvert.SerializeObject(r));
                    }
                }

                if ((obj.TYPE) == ("CALCULATING"))
                {
                    // spedisco pacchetto TYPE: CALCULATING, DATA: data corrente in mill dal 1970
                    DateTime baseDate = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    TimeSpan diff = DateTime.UtcNow - baseDate;
                    var r = new Response { TYPE = "CALCULATING", DATA = (long)(diff.TotalMilliseconds) };
                    context.Send(JsonConvert.SerializeObject(r));
                }

                if ((obj.TYPE) == ("OFFSET_CALCULATION_COMPLETE"))
                {
                    int id = obj.MACHINE_ID;
                    //devo salvare valore ID dell'EyeTracker dentro il file xml
                    XmlWriterSettings settings = new XmlWriterSettings();
                    settings.Indent = true;
                    XmlWriter writer = XmlWriter.Create("Settings.xml", settings);
                    writer.WriteStartDocument();
                    writer.WriteComment("This file is generated by the program.");
                    writer.WriteStartElement("EyeTracker");
                    writer.WriteAttributeString("ID", id.ToString());
                    writer.WriteEndElement();
                    writer.WriteEndDocument();
                    writer.Flush();
                    writer.Close();
                    var r = new Response { TYPE = "READY_TO_PLAY", DATA = "" };
                    context.Send(JsonConvert.SerializeObject(r));
                }

                if ((obj.TYPE) == ("TRAINING_SESSION"))
                {
                    int pID = obj.PATIENT_ID;
                    if (IDSession == pID )
                    {
                        var r = new Response { TYPE = "TRAINING_SESSION", DATA = "false" };
                        context.Send(JsonConvert.SerializeObject(r));
                    }
                    else
                    {
                        IDSession = pID;
                        var r = new Response { TYPE = "TRAINING_SESSION", DATA = "true" };
                        context.Send(JsonConvert.SerializeObject(r));
                    }
                }

                if ((obj.TYPE) == ("SCREEN_MEASURES"))
                {
                    receivedWidth = obj.SCREEN_WIDTH;
                    receivedHeight = obj.SCREEN_HEIGHT;
                    gtClient.Calibration.AreaSize(receivedWidth, receivedHeight);
                }

                if ((obj.TYPE) == ("START_TRAINING"))
                {
                    CalibrationParameters parametri = new CalibrationParameters();
                    parametri.NumberOfPoints = obj.POINTS;
                    parametri.PointDiameter = obj.POINT_DIAMETER;
                    parametri.PointDuration = obj.POINT_DURATION;
                    parametri.PointTransitionDuration = obj.TRANSITION_DURATION;
                    gtClient.Calibration.CalibrationParameters(parametri);
                    gtClient.Calibration.Start();
                }

                if ((obj.TYPE) == ("TRAINING_VALIDATION"))
                {
                    if ((obj.DATA) == "true")
                        gtClient.Calibration.Validate();
                    else
                        gtClient.Calibration.Abort();
                }

                if ((obj.TYPE) == ("START_WORKING"))
                {
                    // Devo Partire tra START_TIME - ora attuale millisecondi
                    StartTime = new DateTime(1970, 01, 01).AddMilliseconds((double)obj.START_TIME);
                    TimeSpan time = StartTime - DateTime.UtcNow;
                    System.Threading.Thread.Sleep(time);
                    gtClient.Stream.StreamStart();
                    onStream = true;
                }

                if ((obj.TYPE) == ("STOP_GAME"))
                {
                    gtClient.Stream.StreamStop();
                    onStream = false;
                }


            }
            catch (Exception e) // Bad JSON! For shame.
            {
                var r = new Response { TYPE = "Errore: ", DATA = new { e.Message } };

                context.Send(JsonConvert.SerializeObject(r));
            }
        }
        // End

        /// <summary>
        /// Broadcasts an error message to the client who caused the error
        /// </summary>
        /// <param name="errorMessage">Details of the error</param>
        /// <param name="context">The user's connection context</param>
        private static void SendError(string errorMessage, UserContext context)
        {
            var r = new Response { TYPE = errorMessage, DATA = new { Message = errorMessage } };

            context.Send(JsonConvert.SerializeObject(r));
        }

        /// <summary>
        /// Broadcasts a message to the client (Before you MUST receive a packege to set the UserContext cont)
        /// </summary>
        /// <param name="type">Message type</param>
        /// <param name="data">Message data</param>
        private static void SendMessage(string type, string data)
        {
            var r = new Response { TYPE = type, DATA = data };
            if (cont != null)
            cont.Send(JsonConvert.SerializeObject(r));
        }

        /// <summary>
        /// Broadcasts a GazeData message to the client (Before you MUST receive a packege to set the UserContext cont)
        /// </summary>
        /// <param name="type">Message type</param>
        /// <param name="data">Message time</param>
        /// <param name="data">Message data</param>
        private static void SendMessageData(string type, long time, string data)
        {
            var r = new ResponseData { TYPE = type, TIME = time, DATA = data };
            if (cont != null)
                cont.Send(JsonConvert.SerializeObject(r));
        }

        /// <summary>
        /// Notify an error to the UI
        /// </summary>
        private void NotifyError(Exception ex)
        {
            e = new ErrorEventArgs(ex);
            if (Error != null)
            {
                Error(this, e);
            }
        }

        /// <summary>
        /// Connect the client to the GazeTracker server
        /// </summary>
        private void ConnectGazeClient()
        {
            gtClient.IPAddress = System.Net.IPAddress.Parse("127.0.0.1");
            gtClient.PortReceive = 6666;
            gtClient.PortSend = 5555;
            try
            {
                gtClient.Connect();
            }
            catch (Exception ex)
            {
                NotifyError(ex);
            }
        }

        /// <summary>
        /// Disconnect the client from the GazeTracker server
        /// </summary>
        private static void DisconnectGazeClient()
        {
            gtClient.Disconnect();
        }

        /// <summary>
        /// Create the xml file where saving EyeTracker ID; Default ID=""
        /// </summary>
        private static void createXml()
        {
            if (!File.Exists("Settings.xml"))
            {
                XmlWriterSettings settings = new XmlWriterSettings();
                settings.Indent = true;
                XmlWriter writer = XmlWriter.Create("Settings.xml", settings);
                writer.WriteStartDocument();
                writer.WriteComment("This file is generated by the program.");
                writer.WriteStartElement("EyeTracker");
                writer.WriteAttributeString("ID", "");
                writer.WriteEndElement();
                writer.WriteEndDocument();
                writer.Flush();
                writer.Close();
            }
        }

        #endregion

        #region PUBLIC METHODS

        /// <summary>
        /// Connect GazeTracker and WebSocket clients.
        /// </summary>
        public void WSConnect()
        {
            
            if(!gtClient.IsRunning)
                ConnectGazeClient();
            wsClient.Connect();

        }

        /// <summary>
        /// Disconnect GazeTracker and WebSocket clients.
        /// </summary>
        public void WSDisconnect()
        {
            DisconnectGazeClient();
            wsClient.Disconnect();
        }

        /// <summary>
        /// Tells if WSClient is connected; return boolean.
        /// </summary>
        public bool WSIsConnected()
        {
            return connected;
        }

        /// <summary>
        /// Return the GazeTracker client.
        /// </summary>
        public GazeTrackerClient.Client getClient()
        {
            return gtClient;
        }

        /// <summary>
        /// Set the screen resolution of the tracking monitor.
        /// </summary>
        public void SetScreenResolution(int width, int height)
        {
            screenWidth = width;
            screenHeight = height;
        }

        public string log()
        {
            return logInfo;
        }

        #endregion

        #region OBJECTS

        /// <summary>
        /// Defines the response object to send back to the client
        /// </summary>
        public class Response
        {
            public string TYPE { get; set; }
            public dynamic DATA { get; set; }
        }

        /// <summary>
        /// Defines the GazeData object to send back to the client
        /// </summary>
        public class ResponseData
        {
            public string TYPE { get; set; }
            public dynamic TIME { get; set; }
            public dynamic DATA { get; set; }
        }

        #endregion

    }
}
