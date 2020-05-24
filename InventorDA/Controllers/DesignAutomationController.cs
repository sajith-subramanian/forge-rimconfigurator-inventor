///////////////////////////////////////////////////////////////////////
//// Copyright (c) Autodesk, Inc. All rights reserved
//// Written by Forge Partner Development
////
//// Permission to use, copy, modify, and distribute this software in
//// object code form for any purpose and without fee is hereby granted,
//// provided that the above copyright notice appears in all copies and
//// that both that copyright notice and the limited warranty and
//// restricted rights notice below appear in all supporting
//// documentation.
////
//// AUTODESK PROVIDES THIS PROGRAM "AS IS" AND WITH ALL FAULTS.
//// AUTODESK SPECIFICALLY DISCLAIMS ANY IMPLIED WARRANTY OF
//// MERCHANTABILITY OR FITNESS FOR A PARTICULAR USE.  AUTODESK, INC.
//// DOES NOT WARRANT THAT THE OPERATION OF THE PROGRAM WILL BE
//// UNINTERRUPTED OR ERROR FREE.
///////////////////////////////////////////////////////////////////////

using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using RestSharp;
using Autodesk.Forge;
using Autodesk.Forge.Model;
using Autodesk.Forge.DesignAutomation;
using Activity = Autodesk.Forge.DesignAutomation.Model.Activity;
using Alias = Autodesk.Forge.DesignAutomation.Model.Alias;
using AppBundle = Autodesk.Forge.DesignAutomation.Model.AppBundle;
using Parameter = Autodesk.Forge.DesignAutomation.Model.Parameter;
using WorkItem = Autodesk.Forge.DesignAutomation.Model.WorkItem;
using WorkItemStatus = Autodesk.Forge.DesignAutomation.Model.WorkItemStatus;
using Autodesk.Forge.DesignAutomation.Model;

namespace InventorDA.Controllers
{
    [ApiController]
    public class DesignAutomationController : ControllerBase
    {
        private IWebHostEnvironment _env;
        private IHubContext<DesignAutomationHub> _hubContext;
        DesignAutomationClient _designAutomation;
        static readonly string EngineName = "Autodesk.Inventor+2021";
        string LocalAppPackageZip { get { return Path.Combine(_env.WebRootPath, @"Bundle/UpdateParamsBundle.zip"); } } 
        static readonly string APPNAME = "inventorDA";
        static readonly string ACTIVITY_NAME = "inventorDActivity";
        static readonly string ALIAS = "v1";
        static readonly string outputIPTFile = "Result.ipt";
        static readonly string outputIDWFile = "Result.idw";
        static readonly string outputPDFile = "Result.pdf";
        public static string nickName { get { return OAuthController.GetAppSetting("FORGE_CLIENT_ID"); } }
    
   
        public DesignAutomationController(IWebHostEnvironment env, IHubContext<DesignAutomationHub> hubContext, DesignAutomationClient api)
        {
            _designAutomation = api;
            _env = env;
            _hubContext = hubContext;
        }
        
        [HttpPost]
        [Route("api/forge/params/designautomation")]
        public async void Post([FromBody] ModelAttributes value)
        {
            await CreateBucket();
            await CreateActivity();
            await CreateWorkItem(value);
        }

        /// <summary>
        /// Creates Activity
        /// </summary>
        private async Task<dynamic> CreateActivity()
        {
            Page<string> appBundles = await _designAutomation.GetAppBundlesAsync();
            string appBundleID = string.Format("{0}.{1}+{2}", nickName, APPNAME, ALIAS);
            if (!appBundles.Data.Contains(appBundleID))
            {
                if (!System.IO.File.Exists(LocalAppPackageZip)) throw new Exception("Appbundle not found at " + LocalAppPackageZip);
                AppBundle appBundleSpec = new AppBundle()
                {
                    Package = APPNAME,
                    Engine = EngineName,
                    Id = APPNAME,
                    Description = string.Format("Description for {0}", APPNAME),
                };
                AppBundle newApp = await _designAutomation.CreateAppBundleAsync(appBundleSpec);
                if (newApp == null) throw new Exception("Cannot create new app");

                // create alias pointing to v1
                Alias aliasSpec = new Alias() { Id = ALIAS, Version = 1 };
                Alias newAlias = await _designAutomation.CreateAppBundleAliasAsync(APPNAME, aliasSpec);

                // upload the zip with .bundle
                RestClient uploadClient = new RestClient(newApp.UploadParameters.EndpointURL);
                RestRequest request = new RestRequest(string.Empty, Method.POST);
                request.AlwaysMultipartFormData = true;
                foreach (KeyValuePair<string, string> x in newApp.UploadParameters.FormData) request.AddParameter(x.Key, x.Value);
                request.AddFile("file", LocalAppPackageZip);
                request.AddHeader("Cache-Control", "no-cache");
                await uploadClient.ExecuteTaskAsync(request);
            }
            Page<string> activities = await _designAutomation.GetActivitiesAsync();
            string qualifiedActivityId = string.Format("{0}.{1}+{2}", nickName, ACTIVITY_NAME, ALIAS);
            if (!activities.Data.Contains(qualifiedActivityId))
            {
                // define the activity
                string commandLine = string.Format(@"$(engine.path)\\inventorcoreconsole.exe /al $(appbundles[{0}].path) $(args[inputJson].path)", APPNAME);
                Activity activitySpec = new Activity()
                {
                    Id = ACTIVITY_NAME,
                    Appbundles = new List<string>() { string.Format("{0}.{1}+{2}", nickName, APPNAME, ALIAS) },
                    CommandLine = new List<string>() { commandLine },
                    Engine = EngineName,
                    Parameters = new Dictionary<string, Parameter>()
                    {
                        { "inputJson", new Parameter() { Description = "input json", LocalName = "params.json", Ondemand = false, Required = false, Verb = Verb.Get, Zip = false } },
                        { "ResultIPT", new Parameter() { Description = "output IPT file", LocalName = outputIPTFile, Ondemand = false, Required = true, Verb = Verb.Put, Zip = false } },
                        { "ResultIDW", new Parameter() { Description = "output IDW file", LocalName = outputIDWFile, Ondemand = false, Required = true, Verb = Verb.Put, Zip = false } },
                        { "ResultPDF", new Parameter() { Description = "output PDF file", LocalName = outputPDFile, Ondemand = false, Required = true, Verb = Verb.Put, Zip = false } }
                    }
                };

                Activity newActivity = await _designAutomation.CreateActivityAsync(activitySpec);

                // specify the alias for this Activity
                Alias aliasSpec = new Alias() { Id = ALIAS, Version = 1 };
                Alias newAlias = await _designAutomation.CreateActivityAliasAsync(ACTIVITY_NAME, aliasSpec);

                return Ok(new { Activity = qualifiedActivityId });
            }
            return Ok(new { Activity = "Activity already defined" });
        }


        // <summary>
        // Creates WorkItem
        // </summary>
        private async Task<IActionResult> CreateWorkItem(ModelAttributes param)
        {
            // OAuth token
            dynamic oauth = await OAuthController.GetInternalAsync();
            string bucketkey = "inventorilogicda" + nickName.ToLower();
            string qualifiedActivityId = string.Format("{0}.{1}+{2}", nickName, ACTIVITY_NAME, ALIAS);
          
            // input json
            dynamic inputJson = new JObject();
            inputJson.Material = param.color;
            inputJson.Diameter = param.diameter;
            inputJson.Spoke_Design = param.rim;
            inputJson.Width = param.width;
            inputJson.InputIPT = "RIM.ipt";
            inputJson.InputIDW = "RIM.idw";

            XrefTreeArgument inputJsonArgument = new XrefTreeArgument()
            {
                Url = "data:application/json, " + ((JObject)inputJson).ToString(Formatting.None).Replace("\"", "'")
            };

            //  output IPT file
            XrefTreeArgument outputIPTFileArgument = new XrefTreeArgument()
            {
                Url = string.Format("https://developer.api.autodesk.com/oss/v2/buckets/{0}/objects/{1}", bucketkey, outputIPTFile),
                Verb = Verb.Put,
                Headers = new Dictionary<string, string>()
                   {
                       {"Authorization", "Bearer " + oauth.access_token }
                   }
            };

            //  output IDW file
            XrefTreeArgument outputIDWFileArgument = new XrefTreeArgument()
            {
                Url = string.Format("https://developer.api.autodesk.com/oss/v2/buckets/{0}/objects/{1}", bucketkey, outputIDWFile),
                Verb = Verb.Put,
                Headers = new Dictionary<string, string>()
                   {
                       {"Authorization", "Bearer " + oauth.access_token }
                   }
            };

            //  output PDF file
            XrefTreeArgument outputPDFFileArgument = new XrefTreeArgument()
            {
                Url = string.Format("https://developer.api.autodesk.com/oss/v2/buckets/{0}/objects/{1}", bucketkey, outputPDFile),
                Verb = Verb.Put,
                Headers = new Dictionary<string, string>()
                   {
                       {"Authorization", "Bearer " + oauth.access_token }
                   }
            };

            string callbackUrl = string.Format("{0}/api/forge/callback/designautomation?id={1}", OAuthController.GetAppSetting("FORGE_WEBHOOK_URL"), param.browserconnectionId);
            WorkItem workItemSpec = new WorkItem()
            {
                ActivityId = qualifiedActivityId,
                Arguments = new Dictionary<string, IArgument>()
                {
                    { "inputJson",  inputJsonArgument },
                    { "ResultIPT", outputIPTFileArgument },
                    { "ResultIDW", outputIDWFileArgument },
                    { "ResultPDF", outputPDFFileArgument },
                    { "onComplete", new XrefTreeArgument { Verb = Verb.Post, Url = callbackUrl } }
                }
            };
            WorkItemStatus workItemStatus = await _designAutomation.CreateWorkItemAsync(workItemSpec);
            return Ok(new { WorkItemId = workItemStatus.Id });
        }

        /// <summary>
        /// Callback from Design Automation Workitem
        /// </summary>
        [HttpPost]
        [Route("/api/forge/callback/designautomation")]
        public async Task<IActionResult> OnCallback(string id/*, [FromBody]dynamic body*/)
        {
            try
            {
                dynamic oauth = await OAuthController.GetInternalAsync();
                string bucketkey = "inventorilogicda" + nickName.ToLower();

                ObjectsApi objectsApi = new ObjectsApi();
                objectsApi.Configuration.AccessToken = oauth.access_token;
                dynamic objIPT = await objectsApi.GetObjectDetailsAsync(bucketkey, outputIPTFile);
                dynamic objIDW = await objectsApi.GetObjectDetailsAsync(bucketkey, outputIDWFile);
                
                dynamic urnIPT = TranslateObject(objIPT, outputIPTFile);
                dynamic urnIDW = TranslateObject(objIDW, outputIDWFile);

                await _hubContext.Clients.Client(id).SendAsync("onTranslate", (string)await urnIPT, (string) await urnIDW);
             
                dynamic signedIPTUrl = objectsApi.CreateSignedResourceAsyncWithHttpInfo(bucketkey, outputIPTFile, new PostBucketsSigned(10), "read");
                dynamic signedIDWUrl = objectsApi.CreateSignedResourceAsyncWithHttpInfo(bucketkey, outputIDWFile, new PostBucketsSigned(10), "read");
                dynamic signedPDFUrl = objectsApi.CreateSignedResourceAsyncWithHttpInfo(bucketkey, outputPDFile, new PostBucketsSigned(10), "read");

                await _hubContext.Clients.Client(id).SendAsync("downloadResult", (string)(await signedIPTUrl).Data.signedUrl, (string)(await signedIDWUrl).Data.signedUrl, (string)(await signedPDFUrl).Data.signedUrl);
                return Ok();

            }
            catch (Exception e) { }
            return Ok();
        }

        /// <summary>
        /// Create Bucket
        /// </summary>
        private async Task<IActionResult> CreateBucket()
        {
            dynamic oauth = await OAuthController.GetInternalAsync();
            string bucketkey = "inventorilogicda" + nickName.ToLower();
            BucketsApi bucketsApi = new BucketsApi();
            bucketsApi.Configuration.AccessToken = oauth.access_token;
            dynamic buckets = await bucketsApi.GetBucketsAsync();
            bool bucketExists = buckets.items.ToString().Contains(bucketkey);
            if (!bucketExists)
            {
                PostBucketsPayload postBucket = new PostBucketsPayload(bucketkey, null, PostBucketsPayload.PolicyKeyEnum.Transient);
                dynamic newbucket = await bucketsApi.CreateBucketAsync(postBucket);
            }
            return Ok();
        }

        /// <summary>
        /// Translate object
        /// </summary>
        private async Task<dynamic> TranslateObject(dynamic objModel,string outputFileName)
        {
            dynamic oauth = await OAuthController.GetInternalAsync();
            string objectIdBase64 = ToBase64(objModel.objectId);
            // prepare the payload
            List<JobPayloadItem> postTranslationOutput = new List<JobPayloadItem>()
            {
            new JobPayloadItem(
                JobPayloadItem.TypeEnum.Svf,
                new List<JobPayloadItem.ViewsEnum>()
                {
                JobPayloadItem.ViewsEnum._2d,
                JobPayloadItem.ViewsEnum._3d
                })
            };
            JobPayload job;
            job = new JobPayload(
                new JobPayloadInput(objectIdBase64,false, outputFileName), 
                new JobPayloadOutput(postTranslationOutput)
                );

            // start the translation
            DerivativesApi derivative = new DerivativesApi();
            derivative.Configuration.AccessToken = oauth.access_token;
            dynamic jobPosted = await derivative.TranslateAsync(job,true);
            // check if it is complete.
            dynamic manifest = null;
            do
            {
                System.Threading.Thread.Sleep(1000); // wait 1 second
                try
                {
                     manifest = await derivative.GetManifestAsync(objectIdBase64);
                }
                catch (Exception) { }
            } while (manifest.progress != "complete");
            return jobPosted.urn;
        }

        /// <summary>
        /// Convert a string into Base64 (source http://stackoverflow.com/a/11743162).
        /// </summary>  
        private static string ToBase64(string input)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(input);
            return System.Convert.ToBase64String(plainTextBytes);
        }

        /// <summary>
        /// Class used for Inputs
        /// </summary>
        public class ModelAttributes
        {
            public string rim { get; set; }
            public string color { get; set; }
            public string diameter { get; set; }
            public string width { get; set; }
            public string browserconnectionId { get; set; }
        }
        
    }
    /// <summary>
    /// Class used for SignalR
    /// </summary>
    public class DesignAutomationHub : Microsoft.AspNetCore.SignalR.Hub
    {
        public string GetConnectionId() {return Context.ConnectionId; }
    }
}
