﻿using System;
using System.Collections.Generic;
using MapRendererCL;
using OpenSim.Region.Framework.Scenes;
using log4net;
using OpenSim.Framework;
using System.Reflection;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using OpenSim.Framework.Servers.HttpServer;
using OpenMetaverse;
using FreeImageAPI;
using OpenSim.Services.Interfaces;
//using PrimMesher;


namespace OpenSim.ApplicationPlugins.MapDataAdapter
{
    /// <summary>
    /// generate map tiles from scene data
    /// </summary>
    public class MapDataAdapter : IApplicationPlugin
    {
        #region member

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // TODO: required by IPlugin, but likely not at all right 
        private string m_name = "MapDataAdapter";
        private string m_version = "0.0";
        protected OpenSimBase m_openSim;
        private BaseHttpServer m_server;
        private List<MapRegion> m_regions;
        public string Version
        {
            get { return m_version; }
        }
        public string Name
        {
            get { return m_name; }
        }


        #endregion

        public void Initialise()
        {
            //用于处理opensim 没有被正确初始化的情况 
            m_log.Error("[APPPLUGIN]: " + Name + " cannot be default-initialized!");
            throw new PluginNotInitialisedException(Name);
        }
        public void Initialise(OpenSimBase openSim)
        {
            //将opensim的引用记为内部成员变量 
            m_log.Info("[MapDataAdapter]: initialized!");
            m_openSim = openSim;
            m_server = openSim.HttpServer;
            m_regions = new List<MapRegion>();
        }
        public void PostInitialise()
        {
            List<Scene> scenelist = m_openSim.SceneManager.Scenes;
            lock (scenelist)
            {
                foreach (Scene scene in scenelist)
                {
                    m_regions.Add(new MapRegion(scene));
                }
            }
            // register ows handler
            string httpMethod = "GET";
            string path = "/map";
            OWSStreamHandler h = new OWSStreamHandler(httpMethod, path, owsHandler);
            m_server.AddStreamHandler(h);
        }

        public void Dispose()
        {

        }

        public MapDataAdapter()
        {
        }

        public string owsHandler(string request, string path, string param,
                                      OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {            
            switch (httpRequest.QueryString["SERVICE"])
            {
                case "WMS":
                    if (httpRequest.QueryString["REQUEST"] == "GetMap")
                    {                        
                        //parse query string
                        string[] layers = httpRequest.QueryString["LAYERS"].Split(',');
                        BBox bbox = new BBox(httpRequest.QueryString["BBOX"]);
                        int height = Int32.Parse(httpRequest.QueryString["HEIGHT"]);
                        int width = Int32.Parse(httpRequest.QueryString["WIDTH"]);                        
                        int elevation = Int32.Parse(httpRequest.QueryString["ELEVATION"]);
                        string regionID = httpRequest.QueryString["REGIONID"];                       
                        foreach (MapRegion region in m_regions)
                        {
                            if (region.ID == regionID)
                            {
                                lock (region)
                                {
                                    region.Elevation = elevation;
                                    region.MapRegionBBox = bbox;
                                    region.MapRegionImg = new MapRegionImage(width, height);
                                    region.initialize(layers);
                                    
                                    Bitmap queryImg = region.generateMapRegionImg();
                                    System.IO.MemoryStream stream = new System.IO.MemoryStream();
                                    queryImg.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                                    queryImg.Dispose();
                                    byte[] byteImage = stream.ToArray();
                                    stream.Close();
                                    stream.Dispose();
                                    httpResponse.ContentType = "image/png";
                                    return Convert.ToBase64String(byteImage);
                                }
                            }
                        }
                    
                    httpResponse.ContentType = "text/plain";
                    return "Something unexpected occurs!";                                          
                    }
                    else if (httpRequest.QueryString["REQUEST"] == "GetCapabilities")
                    {
                        httpResponse.ContentType = "text/xml";
                        string capDes = "";
                        TextReader textReader = new StreamReader("Capability.xml");
                        capDes = textReader.ReadToEnd();
                        textReader.Close();
                        return capDes;
                    }
                    else
                    {
                        return "Sorry, the request method is not supported by this service.";
                    }
                //case "WFS":
                //    if (httpRequest.QueryString["REQUEST"] == "GetFeature")
                //    {
                //        if ((httpRequest.QueryString["TypeName"] == "agent"))
                //        {
                //            switch (httpRequest.QueryString["FORMAT"])
                //            { 
                //                case "text":
                //                    return m_agentLayer.GetFeaturesByText();
                //                case "xml":
                //                    return m_agentLayer.GetFeaturesByXML();
                //            }
                //        }
                //        else
                //            return "Query String is not supported";
                //    }
                //    break;
                //default:
                //    return "Unsupport Service";
            }
            return "Unsupported Service";
        }   
    }
}
