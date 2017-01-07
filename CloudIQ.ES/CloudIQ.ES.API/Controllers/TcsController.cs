using CloudIQ.ES.API.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Web;
using System.Web.Http;

namespace CloudIQ.ES.API.Controllers
{
    public class TcsController : ApiController
    {
        //public string GetAccounts()
        //{
        //    try
        //    {
        //        //return Common.GetAccountCsvFile();
        //    }
        //    catch (Exception ex)
        //    {
        //        return ex.Message;            
        //    }                        
        //}

        public HttpResponseMessage GetAccounts()
        {
            try
            {
                HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new StreamContent(Common.GetAccountCsvFile());
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
                response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
                {
                    FileName = "accounts.csv"
                };
                return response;
            }
            catch(Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        public HttpResponseMessage GetVpcs()
        {
            try
            {
                HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new StreamContent(Common.GetVpcCsvFile());
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
                response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
                {
                    FileName = "vpcs.csv"
                };
                return response;
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }
    }
}