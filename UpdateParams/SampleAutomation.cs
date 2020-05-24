/////////////////////////////////////////////////////////////////////
// Copyright (c) Autodesk, Inc. All rights reserved
// Written by Forge Partner Development
//
// Permission to use, copy, modify, and distribute this software in
// object code form for any purpose and without fee is hereby granted,
// provided that the above copyright notice appears in all copies and
// that both that copyright notice and the limited warranty and
// restricted rights notice below appear in all supporting
// documentation.
//
// AUTODESK PROVIDES THIS PROGRAM "AS IS" AND WITH ALL FAULTS.
// AUTODESK SPECIFICALLY DISCLAIMS ANY IMPLIED WARRANTY OF
// MERCHANTABILITY OR FITNESS FOR A PARTICULAR USE.  AUTODESK, INC.
// DOES NOT WARRANT THAT THE OPERATION OF THE PROGRAM WILL BE
// UNINTERRUPTED OR ERROR FREE.
/////////////////////////////////////////////////////////////////////

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Inventor;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Text;
using System.IO;
using System.Reflection;

namespace UpdateParams
{
    [ComVisible(true)]
    public class SampleAutomation
    {
        InventorServer m_inventorServer;

        public SampleAutomation(InventorServer inventorServer)
        {
            Trace.TraceInformation("Starting sample plugin.");
            m_inventorServer = inventorServer;
        }

        public void Run(Document doc)
        {

            Trace.TraceInformation("Running with no Args.");
            NameValueMap map = m_inventorServer.TransientObjects.CreateNameValueMap();
            RunWithArguments(doc, map);

        }

        public void RunWithArguments(Document doc, NameValueMap map)
        {
            try
            {
                StringBuilder traceInfo = new StringBuilder("RunWithArguments called with ");
                Trace.TraceInformation(map.Count.ToString());
                // values in map are keyed on _1, _2, etc
                for (int i = 0; i < map.Count; i++)
                {
                    traceInfo.Append(" and ");
                    traceInfo.Append(map.Value["_" + (i + 1)]);
                }
                Trace.TraceInformation(traceInfo.ToString());

                #region change parameters
                Trace.TraceInformation("Changing User params");

                // load processing parameters
                string paramsJson = GetParametersToChange(map);
                Trace.TraceInformation("Inventor Parameters JSON: \"" + paramsJson + "\"");
                Dictionary<string, string> parameters = JsonConvert.DeserializeObject<Dictionary<string, string>>(paramsJson);

                // Get path of add-in dll
                string assemblyPath = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                Trace.TraceInformation("Assembly Path = " + assemblyPath);

                // Path of template relative to the dll's path
                string iptPath = parameters["InputIPT"];
                string idwPath = parameters["InputIDW"];

                // Open template document
                string iptfullPath = System.IO.Path.Combine(assemblyPath, @"Rim\", iptPath);
                string idwfullPath = System.IO.Path.Combine(assemblyPath, @"Rim\", idwPath);
                Document iptDoc = m_inventorServer.Documents.Open(iptfullPath, false);
                Document idwDoc = m_inventorServer.Documents.Open(idwfullPath, false);
               
                var theParams = GetParameters(iptDoc);
                foreach (KeyValuePair<string, string> entry in parameters)
                {
                    var parameterName = entry.Key;
                    var value = entry.Value;
                    Trace.TraceInformation("Parameter to change: {0}:{1}", parameterName, value);
                    try
                    {
                        UserParameter param = theParams[parameterName];
                        try { param.Value = value; }
                        catch { param.Expression = value; } 
                    }
                    catch (Exception e)
                    {
                        Trace.TraceInformation("Cannot update '{0}' parameter. ({1})", parameterName, e.Message);
                    }
                }
                iptDoc.Update();
                Trace.TraceInformation("Part Doc updated.");
                var currDir = System.IO.Directory.GetCurrentDirectory();
                var iptfileName = System.IO.Path.Combine(currDir, "Result.ipt"); // the name must be in sync with OutputIpt localName in Activity
                iptDoc.SaveAs(iptfileName, false);
                Trace.TraceInformation(" Part Doc saved.");

                idwDoc.Update();
                Trace.TraceInformation("Drawing Doc updated.");
                var idwfileName = System.IO.Path.Combine(currDir, "Result.idw"); // the name must be in sync with OutputIdw localName in Activity
                idwDoc.SaveAs(idwfileName, false);
                var pdffilename = System.IO.Path.Combine(currDir, "Result.pdf"); // name must be in sync with OutputPDF localName in Activity
                idwDoc.SaveAs(pdffilename, true);
                Trace.TraceInformation(" Drawing Doc saved.");

                #endregion
            }
            catch (Exception ex)
            { Trace.TraceInformation(ex.Message); }
        }

        private static string GetParametersToChange(NameValueMap map)
        {
            string paramFile = (string)map.Value["_1"];
            string json = System.IO.File.ReadAllText(paramFile);
            return json;
        }

        private static UserParameters GetParameters(Document doc)
        {
            var docType = doc.DocumentType;
            switch (docType)
            {
                case DocumentTypeEnum.kAssemblyDocumentObject:
                    var asm = doc as AssemblyDocument;
                    return asm.ComponentDefinition.Parameters.UserParameters;

                case DocumentTypeEnum.kPartDocumentObject:
                    var ipt = doc as PartDocument;
                    return ipt.ComponentDefinition.Parameters.UserParameters;

                default:
                    throw new ApplicationException(string.Format("Unexpected document type ({0})", docType));
            }
        }        
    }
}
