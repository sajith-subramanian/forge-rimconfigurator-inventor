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

namespace UpdateParams
{
    [Guid("92DDE0B3-A219-4C18-B28F-F2136738F475")]
    public class PluginServer : ApplicationAddInServer
    {
        public PluginServer()
        {
        }

        // Inventor application object.
        InventorServer m_inventorServer;
        SampleAutomation m_automation;

        public dynamic Automation
        {
            get
            {
                return m_automation;
            }
        }

        public void Activate(ApplicationAddInSite AddInSiteObject, bool FirstTime)
        {
            Trace.TraceInformation("Update Param Plugin: initializing... ");

            // Initialize AddIn members.
            m_inventorServer = AddInSiteObject.InventorServer;
            m_automation = new SampleAutomation(m_inventorServer);
        }

        public void Deactivate()
        {
            Trace.TraceInformation("Update Param Plugin: deactivating... ");

            // Release objects.
            Marshal.ReleaseComObject(m_inventorServer);
            m_inventorServer = null;

            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        public void ExecuteCommand(int CommandID)
        {
            // obsolete
        }
    }
}
