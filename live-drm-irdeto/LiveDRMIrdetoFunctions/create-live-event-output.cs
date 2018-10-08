//
// Azure Media Services REST API v3 Functions
//
// create-live-event-output - This function creates a new live event and output (to be used with Irdeto)
//
/*
```c#
Input :
{
    "liveEventName": "SFPOC",
    "storageAccountName" : "" // optional. Specify in which attached storage account the asset should be created. If azureRegion is specified, then the region is appended to the name
    "inputProtocol" : "FragmentedMP4" or "RTMP"  // value is optional. Default is FragmentedMP4
    "vanityUrl" : true // VanityUrl if true then LiveEvent has a predictable ingest URL even when stopped. It takes more time to get it. Non Vanity URL Live Event are quicker to get, but ingest is only known when the live event is running
    "archiveWindowLength" : 20  // value in minutes, optional. Default is 10 (minutes)
    "liveEventAutoStart": False  // optional. Default is True
    "azureRegion": "euwe" or "we" or "euno" or "no"// optional. If this value is set, then the AMS account name and resource group are appended with this value. Usefull if you want to manage several AMS account in different regions. Note: the service principal must work with all this accounts
    "useDRM" : true // optional. Default is true. Specify false if you don't want to use dynamic encryption
    "InputACL": [  // optional
                "192.168.0.1/24"
            ],
    "PreviewACL": [ // optional
                "192.168.0.0/24"
            ],
}


Output:
{
    "Success": true,
    "LiveEvents": [
        {
            "Name": "TEST",
            "ResourceState": "Running",
            "vanityUrl": true,
            "Input": [
                {
                    "Protocol": "FragmentedMP4",
                    "Url": "http://test-prodliveeuwe-euwe.channel.media.azure.net/fe21a7147fb64498b52f024c41a3298e/ingest.isml"
                }
            ],
            "InputACL": [
                "192.168.0.1/24"
            ],
            "Preview": [
                {
                    "Protocol": "DashCsf",
                    "Url": "https://test-prodliveeuwe.preview-euwe.channel.media.azure.net/fbc40c48-07bd-4938-92f2-3375597d8ce3/preview.ism/manifest(format=mpd-time-csf)"
                }
            ],
            "PreviewACL": [
                "192.168.0.0/24"
            ],
            "LiveOutputs": [
                {
                    "Name": "output-8b49c322-8429",
                    "ArchiveWindowLength": 10,
                    "AssetName": "asset-8b49c322-8429",
                    "AssetStorageAccountName": "lsvdefaultdeveuwe",
                    "ResourceState": "Running",
                    "StreamingLocatorName": "locator-8b49c322-8429",
                    "StreamingPolicyName": "TEST-dd5a9c6b-b159",
                    "Drm": [
                        {
                            "Type": "FairPlay",
                            "LicenseUrl": "skd://rng.live.ott.irdeto.com/licenseServer/streaming/v1/SRG/getckc?ContentId=SRF2&KeyId=dd5a9130-9734-45b4-945b-57516ee80945"
                        },
                        {
                            "Type": "PlayReady",
                            "LicenseUrl": "https://rng.live.ott.irdeto.com/licenseServer/playready/v1/CUSTOMER/license?ContentId=ID2"
                        },
                        {
                            "Type": "Widevine",
                            "LicenseUrl": "https://rng.live.ott.irdeto.com/licenseServer/widevine/v1/CUSTOMER/license&ContentId=ID2"
                        }
                    ],
                    "CencKeyId": "3391a2a8-43e1-48e6-9d0b-39dd12a1d300",
                    "CbcsKeyId": "dd5a9130-9734-45b4-945b-57516ee80945",
                    "Urls": [
                        {
                            "Url": "https://prodliveeuwe-prodliveeuwe-euwe.streaming.media.azure.net/8d61c393-87dc-488b-a886-9adf9ba5bafc/test.ism/manifest(encryption=cenc)",
                            "Protocol": "SmoothStreaming"
                        },
                        {
                            "Url": "https://prodliveeuwe-prodliveeuwe-euwe.streaming.media.azure.net/8d61c393-87dc-488b-a886-9adf9ba5bafc/test.ism/manifest(format=mpd-time-csf,encryption=cenc)",
                            "Protocol": "DashCsf"
                        },
                        {
                            "Url": "https://prodliveeuwe-prodliveeuwe-euwe.streaming.media.azure.net/8d61c393-87dc-488b-a886-9adf9ba5bafc/test.ism/manifest(format=mpd-time-cmaf,encryption=cenc)",
                            "Protocol": "DashCmaf"
                        },
                        {
                            "Url": "https://prodliveeuwe-prodliveeuwe-euwe.streaming.media.azure.net/8d61c393-87dc-488b-a886-9adf9ba5bafc/test.ism/manifest(format=m3u8-cmaf,encryption=cbcs-aapl)",
                            "Protocol": "HlsCmaf"
                        },
                        {
                            "Url": "https://prodliveeuwe-prodliveeuwe-euwe.streaming.media.azure.net/8d61c393-87dc-488b-a886-9adf9ba5bafc/test.ism/manifest(format=m3u8-aapl,encryption=cbcs-aapl)",
                            "Protocol": "HlsTs"
                        }
                    ]
                }
            ]
        }
    ]
}
```
*/
//
//

using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.Management.Media;
using System.Threading.Tasks;
using Microsoft.Azure.Management.Media.Models;
using System;
using System.Collections.Generic;
using System.Net;
using System.Linq;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Logging;
using LiveDrmOperationsV3.Models;
using LiveDrmOperationsV3.Helpers;


namespace LiveDrmOperationsV3
{
    public static class CreateChannel
    {
        // This version registers keys in irdeto backend. For FairPlay and rpv3

        [FunctionName("create-live-event-output")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequest req, ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string requestBody = new StreamReader(req.Body).ReadToEnd();
            dynamic data = JsonConvert.DeserializeObject(requestBody);

            ConfigWrapper config = null;

            try
            {
                config = new ConfigWrapper(
                     new ConfigurationBuilder()
                     .SetBasePath(Directory.GetCurrentDirectory())
                     .AddEnvironmentVariables()
                     .Build(),
                      data.azureRegion != null ? (string)data.azureRegion : null
             );
            }
            catch (Exception ex)
            {
                return IrdetoHelpers.ReturnErrorException(log, ex);
            }

            log.LogInformation("config loaded.");

            string liveEventName = (string)data.liveEventName;
            if (liveEventName == null)
            {
                return IrdetoHelpers.ReturnErrorException(log, "Error - please pass liveEventName in the JSON");
            }

            // default settings
            LiveEventSettingsInfo eventInfoFromCosmos = new LiveEventSettingsInfo()
            {
                liveEventName = liveEventName
            };

            // Load config from Cosmos
            try
            {
                var helper = new CosmosHelpers(log, config);
                eventInfoFromCosmos = await helper.ReadSettingsDocument(liveEventName) ?? eventInfoFromCosmos;
            }
            catch (Exception ex)
            {
                return IrdetoHelpers.ReturnErrorException(log, ex);
            }

            // init default

            StreamingPolicy streamingPolicy = null;
            string uniqueness = Guid.NewGuid().ToString().Substring(0, 13);
            string streamingLocatorName = "locator-" + uniqueness;
            string manifestName = liveEventName.ToLower();

            bool useDRM = (data.useDRM != null) ? (bool)data.useDRM : true;
            Asset asset = null;
            LiveEvent liveEvent = null;
            LiveOutput liveOutput = null;


            if (data.archiveWindowLength != null)
            {
                eventInfoFromCosmos.archiveWindowLength = (int)data.archiveWindowLength;
            }

            if (eventInfoFromCosmos.baseStorageName != null)
            {
                eventInfoFromCosmos.StorageName = eventInfoFromCosmos.baseStorageName + config.AzureRegionCode;
            }

            if (data.storageAccountName != null)
            {
                eventInfoFromCosmos.StorageName = (string)data.storageAccountName;
            }

            if (data.inputProtocol != null && ((string)data.inputProtocol).ToUpper() == "RTMP")
            {
                eventInfoFromCosmos.inputProtocol = LiveEventInputProtocol.RTMP;
            }

            if (data.liveEventAutoStart != null)
            {
                eventInfoFromCosmos.autoStart = (bool)data.liveEventAutoStart;
            }

            if (data.InputACL != null)
            {
                eventInfoFromCosmos.liveEventInputACL = (List<string>)data.InputACL;
            }

            if (data.PreviewACL != null)
            {
                eventInfoFromCosmos.liveEventPreviewACL = (List<string>)data.PreviewACL;
            }

            IAzureMediaServicesClient client = await MediaServicesHelpers.CreateMediaServicesClientAsync(config);
            // Set the polling interval for long running operations to 2 seconds.
            // The default value is 30 seconds for the .NET client SDK
            client.LongRunningOperationRetryTimeout = 2;


            // LIVE EVENT CREATION
            log.LogInformation("Live event creation...");

            try
            {
                // let's check that the channel does not exist already
                liveEvent = await client.LiveEvents.GetAsync(config.ResourceGroup, config.AccountName, liveEventName);
                if (liveEvent != null)
                {
                    return IrdetoHelpers.ReturnErrorException(log, "Error : live event already exists !");
                }

                // IP ACL for preview URL
                //var ipAclListPreview = config.LiveEventPreviewACL?.Trim().Split(';').ToList();
                List<IPRange> ipsPreview = new List<Microsoft.Azure.Management.Media.Models.IPRange>();
                if (eventInfoFromCosmos.liveEventPreviewACL == null || eventInfoFromCosmos.liveEventPreviewACL.Count == 0)
                {
                    log.LogInformation("preview all");
                    IPRange ip = new Microsoft.Azure.Management.Media.Models.IPRange() { Name = "AllowAll", Address = IPAddress.Parse("0.0.0.0").ToString(), SubnetPrefixLength = 0 };
                    ipsPreview.Add(ip);
                }
                else
                {
                    foreach (var ipacl in eventInfoFromCosmos.liveEventPreviewACL)
                    {
                        var ipaclcomp = ipacl.Split('/');  // notation can be "192.168.0.1" or "192.168.0.1/32"
                        int subnet = ipaclcomp.Count() > 1 ? Convert.ToInt32(ipaclcomp[1]) : 0;
                        IPRange ip = new Microsoft.Azure.Management.Media.Models.IPRange() { Name = "ip", Address = IPAddress.Parse(ipaclcomp[0]).ToString(), SubnetPrefixLength = subnet };
                        ipsPreview.Add(ip);
                    }
                }

                var liveEventPreview = new LiveEventPreview
                {
                    AccessControl = new LiveEventPreviewAccessControl(ip: new IPAccessControl(allow: ipsPreview))
                };

                var liveEventInput = new LiveEventInput(eventInfoFromCosmos.inputProtocol);

                // IP ACL for input URL
                //var ipAclListInput = config.LiveEventInputACL?.Trim().Split(';').ToList();
                List<IPRange> ipsInput = new List<Microsoft.Azure.Management.Media.Models.IPRange>();

                //  if (config.LiveEventInputACL == null || config.LiveEventInputACL.Trim() == "" || ipAclListInput == null || ipAclListInput.Count == 0)
                if (eventInfoFromCosmos.liveEventInputACL == null || eventInfoFromCosmos.liveEventInputACL.Count == 0)
                {
                    log.LogInformation("input all");
                    IPRange ip = new Microsoft.Azure.Management.Media.Models.IPRange() { Name = "AllowAll", Address = IPAddress.Parse("0.0.0.0").ToString(), SubnetPrefixLength = 0 };
                    ipsInput.Add(ip);
                }
                else
                {
                    foreach (var ipacl in eventInfoFromCosmos.liveEventInputACL)
                    {
                        var ipaclcomp = ipacl.Split('/');  // notation can be "192.168.0.1" or "192.168.0.1/32"
                        int subnet = ipaclcomp.Count() > 1 ? Convert.ToInt32(ipaclcomp[1]) : 0;
                        IPRange ip = new Microsoft.Azure.Management.Media.Models.IPRange() { Name = "ip", Address = IPAddress.Parse(ipaclcomp[0]).ToString(), SubnetPrefixLength = subnet };
                        ipsInput.Add(ip);
                    }
                }
                liveEventInput.AccessControl = new LiveEventInputAccessControl(ip: new IPAccessControl(allow: ipsInput));

                liveEvent = new LiveEvent(
                                           name: liveEventName,
                                           location: config.Region,
                                           description: "",
                                           vanityUrl: eventInfoFromCosmos.vanityUrl,
                                           encoding: new LiveEventEncoding() { EncodingType = LiveEventEncodingType.None },
                                           input: liveEventInput,
                                           preview: liveEventPreview,
                                           streamOptions: new List<StreamOptionsFlag?>()
                                           {
                                            // Set this to Default or Low Latency
                                            StreamOptionsFlag.Default
                                           }
                                         );


                liveEvent = await client.LiveEvents.CreateAsync(config.ResourceGroup, config.AccountName, liveEventName, liveEvent, autoStart: eventInfoFromCosmos.autoStart);
                log.LogInformation("Live event created.");
            }
            catch (Exception ex)
            {
                return IrdetoHelpers.ReturnErrorException(log, ex, "live event creation error");
            }

            if (useDRM)
            {
                // STREAMING POLICY CREATION
                log.LogInformation("Creating streaming policy.");
                try
                {
                    streamingPolicy = await IrdetoHelpers.CreateStreamingPolicyIrdeto(liveEventName, config, client);
                }
                catch (Exception ex)
                {
                    return IrdetoHelpers.ReturnErrorException(log, ex, "streaming policy creation error");
                }
            }

            // LIVE OUTPUT CREATION
            log.LogInformation("Live output creation...");

            try
            {
                log.LogInformation("Asset creation...");

                asset = await client.Assets.CreateOrUpdateAsync(config.ResourceGroup, config.AccountName, "asset-" + uniqueness, new Asset(storageAccountName: eventInfoFromCosmos.StorageName, description: IrdetoHelpers.SetLocatorNameInDescription(streamingLocatorName)));

                Hls hlsParam = null;

                liveOutput = new LiveOutput(asset.Name, TimeSpan.FromMinutes((double)eventInfoFromCosmos.archiveWindowLength), null, "output-" + uniqueness, null, null, manifestName, hlsParam); //we put the streaming locator in description
                log.LogInformation("await task...");

                log.LogInformation("create live output...");
                await client.LiveOutputs.CreateAsync(config.ResourceGroup, config.AccountName, liveEventName, liveOutput.Name, liveOutput);
                log.LogInformation("Asset created.");
            }
            catch (Exception ex)
            {
                return IrdetoHelpers.ReturnErrorException(log, ex, "live output creation error");
            }

            string cenckeyId = null;
            string cenccontentKey = null;
            string cbcskeyId = null;
            string cbcscontentKey = null;

            if (useDRM)
            {
                try
                {
                    log.LogInformation("Irdeto call...");

                    // CENC Key
                    Guid cencGuid = Guid.NewGuid();
                    cenckeyId = cencGuid.ToString();
                    cenccontentKey = Convert.ToBase64String(IrdetoHelpers.GetRandomBuffer(16));
                    string cencIV = Convert.ToBase64String(cencGuid.ToByteArray());
                    var responsecenc = await IrdetoHelpers.CreateSoapEnvelopRegisterKeys(config.IrdetoSoapService, liveEventName, config, cenckeyId, cenccontentKey, cencIV, false);
                    string contentcenc = await responsecenc.Content.ReadAsStringAsync();

                    if (responsecenc.StatusCode != HttpStatusCode.OK)
                    {
                        return IrdetoHelpers.ReturnErrorException(log, "Error Irdeto response cenc - " + contentcenc);
                    }

                    string cenckeyIdFromIrdeto = IrdetoHelpers.ReturnDataFromSoapResponse(contentcenc, "KeyId=");
                    string cenccontentKeyFromIrdeto = IrdetoHelpers.ReturnDataFromSoapResponse(contentcenc, "ContentKey=");

                    if (cenckeyId != cenckeyIdFromIrdeto || cenccontentKey != cenccontentKeyFromIrdeto)
                    {
                        return IrdetoHelpers.ReturnErrorException(log, "Error CENC not same key - " + contentcenc);
                    }

                    // CBCS Key
                    Guid cbcsGuid = Guid.NewGuid();
                    cbcskeyId = cbcsGuid.ToString();
                    cbcscontentKey = Convert.ToBase64String(IrdetoHelpers.GetRandomBuffer(16));
                    string cbcsIV = Convert.ToBase64String(IrdetoHelpers.HexStringToByteArray(cbcsGuid.ToString().Replace("-", string.Empty)));
                    var responsecbcs = await IrdetoHelpers.CreateSoapEnvelopRegisterKeys(config.IrdetoSoapService, liveEventName, config, cbcskeyId, cbcscontentKey, cbcsIV, true);
                    string contentcbcs = await responsecbcs.Content.ReadAsStringAsync();

                    if (responsecbcs.StatusCode != HttpStatusCode.OK)
                    {
                        return IrdetoHelpers.ReturnErrorException(log, "Error Irdeto response cbcs - " + contentcbcs);
                    }

                    string cbcskeyIdFromIrdeto = IrdetoHelpers.ReturnDataFromSoapResponse(contentcbcs, "KeyId=");
                    string cbcscontentKeyFromIrdeto = IrdetoHelpers.ReturnDataFromSoapResponse(contentcbcs, "ContentKey=");

                    if (cbcskeyId != cbcskeyIdFromIrdeto || cbcscontentKey != cbcscontentKeyFromIrdeto)
                    {
                        return IrdetoHelpers.ReturnErrorException(log, "Error CBCS not same key - " + contentcbcs);
                    }
                    log.LogInformation("Irdeto call done.");
                }
                catch (Exception ex)
                {
                    return IrdetoHelpers.ReturnErrorException(log, ex, "Irdeto response error");
                }
            }


            try
            {
                // let's get the asset
                // in v3, asset name = asset if in v2 (without prefix)
                log.LogInformation("Asset configuration.");

                StreamingLocator locator = null;
                if (useDRM)
                {
                    locator = await IrdetoHelpers.SetupDRMAndCreateLocator(config, streamingPolicy.Name, streamingLocatorName, client, asset, cenckeyId, cenccontentKey, cbcskeyId, cbcscontentKey);
                }
                else // no DRM
                {
                    locator = new StreamingLocator(asset.Name, null);
                    locator = await client.StreamingLocators.CreateAsync(config.ResourceGroup, config.AccountName, streamingLocatorName, locator);
                }

                log.LogInformation("locator : " + locator.Name);

                // await taskLiveOutputCreation;
            }
            catch (Exception ex)
            {
                return IrdetoHelpers.ReturnErrorException(log, ex, "locator creation error");
            }

            // object to store the output of the function
            var generalOutputInfo = new GeneralOutputInfo();

            // let's build info for the live event and output
            try
            {
                generalOutputInfo = GenerateInfoHelpers.GenerateOutputInformation(config, client, new List<LiveEvent>() { liveEvent });
            }

            catch (Exception ex)
            {
                return IrdetoHelpers.ReturnErrorException(log, ex);
            }

            try
            {
                var helper = new CosmosHelpers(log, config);
                await helper.CreateOrUpdateGeneralInfoDocument(generalOutputInfo.LiveEvents[0]);
            }
            catch (Exception ex)
            {
                return IrdetoHelpers.ReturnErrorException(log, ex);
            }

            return (ActionResult)new OkObjectResult(
             JsonConvert.SerializeObject(generalOutputInfo, Formatting.Indented)
               );
        }
    }
}