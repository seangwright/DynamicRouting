using System;
using System.Web;
using System.Web.Mvc;
using CMS.Base;
using CMS.DataEngine;
using CMS.EventLog;
using CMS.Helpers;
using DynamicRouting.Helpers;

using RequestContext = System.Web.Routing.RequestContext;

namespace DynamicRouting.Kentico.MVC
{
    public class DynamicHttpHandler : IHttpHandler
    {
        public RequestContext RequestContext { get; set; }
        public DynamicHttpHandler(RequestContext requestContext) => RequestContext = requestContext;

        public bool IsReusable
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if the current page is using a template or not.
        /// </summary>
        /// <param name="Page">The Tree Node</param>
        /// <returns>If it has a template or not</returns>
        public static bool PageHasTemplate(ITreeNode Page)
        {
            string TemplateConfiguration = ValidationHelper.GetString(Page.GetValue("DocumentPageTemplateConfiguration"), "");

            // Check Temp Page builder widgets to detect a switch in template
            var InstanceGuid = ValidationHelper.GetGuid(URLHelper.GetQueryValue(HttpContext.Current.Request.Url.AbsoluteUri, "instance"), Guid.Empty);
            if (InstanceGuid != Guid.Empty)
            {
                var Table = ConnectionHelper.ExecuteQuery(String.Format("select PageBuilderTemplateConfiguration from Temp_PageBuilderWidgets where PageBuilderWidgetsGuid = '{0}'", InstanceGuid.ToString()), null, QueryTypeEnum.SQLQuery).Tables[0];
                if (Table.Rows.Count > 0)
                {
                    TemplateConfiguration = ValidationHelper.GetString(Table.Rows[0]["PageBuilderTemplateConfiguration"], "");
                }
            }

            return !String.IsNullOrWhiteSpace(TemplateConfiguration) && !TemplateConfiguration.ToLower().Contains("\"empty.template\"");
        }

        public void ProcessRequest(HttpContext context)
        {
            var factory = ControllerBuilder.Current.GetControllerFactory();

            string DefaultController = (RequestContext.RouteData.Values.ContainsKey("controller") ? RequestContext.RouteData.Values["controller"].ToString() : "");
            string DefaultAction = (RequestContext.RouteData.Values.ContainsKey("action") ? RequestContext.RouteData.Values["action"].ToString() : "");
            string NewController = "";
            string NewAction = "Index";
            // Get the classname based on the URL
            var FoundNode = DynamicRouteHelper.GetPage(EnvironmentHelper.GetUrl(context.Request));
            string ClassName = FoundNode.ClassName;


            switch (ClassName.ToLower())
            {

                default:
                    // 
                    if (PageHasTemplate(FoundNode))
                    {
                        // Uses Page Templates, send to basic Page Template handler
                        NewController = "DynamicPageTemplate";
                        NewAction = "Index";
                    }
                    else
                    {
                        // Try finding a class that matches the class name
                        NewController = ClassName.Replace(".", "_");
                    }
                    break;
                // can add your own cases to do more advanced logic if you wish
                case "":
                    break;

            }

            // Controller not found, use defaults
            if (String.IsNullOrWhiteSpace(NewController))
            {
                NewController = DefaultController;
                NewAction = DefaultAction;
            }

            // Setup routing with new values
            RequestContext.RouteData.Values["Controller"] = NewController;

            // If there is an action (2nd value), change it to the CheckNotFound, and remove ID
            if (RequestContext.RouteData.Values.ContainsKey("Action"))
            {
                RequestContext.RouteData.Values["Action"] = NewAction;
            }
            else
            {
                RequestContext.RouteData.Values.Add("Action", NewAction);
            }
            if (RequestContext.RouteData.Values.ContainsKey("Id"))
            {
                RequestContext.RouteData.Values.Remove("Id");
            }

            IController controller;

            try
            {
                controller = factory.CreateController(RequestContext, NewController);
                controller.Execute(RequestContext);
            }
            catch (HttpException ex)
            {
                // Catch Controller Not implemented errors and log and go to Not Foud
                if (ex.Message.ToLower().Contains("does not implement icontroller."))
                {
                    EventLogProvider.LogException("KMVCDynamicHttpHandler", "ClassControllerNotConfigured", ex, additionalMessage: "Page found, but could not find Page Templates, nor a Controller for " + NewController + ", either create Page Templates for this class or create a controller with an index view to auto handle or modify the KMVCDynamicHttpHandler");
                    RequestContext.RouteData.Values["Controller"] = "DynamicPageTemplate";
                    RequestContext.RouteData.Values["Action"] = "NotFound";
                    controller = factory.CreateController(RequestContext, "DynamicPageTemplate");
                    controller.Execute(RequestContext);
                }
                else
                {
                    // This will show for any http generated exception, like view errors
                    throw new HttpException(ex.Message, ex);
                }
            }
            catch (InvalidOperationException ex)
            {
                if (ex.Message.ToLower().Contains("page template with identifier"))
                {
                    // This often occurs when there is a page template assigned that is not defined
                    EventLogProvider.LogException("KMVCDynamicHttpHandler", "ClassControllerNotConfigured", ex, additionalMessage: "Page found, but contains a template that is not registered with this application.");
                    RequestContext.RouteData.Values["Controller"] = "DynamicPageTemplate";
                    RequestContext.RouteData.Values["Action"] = "UnregisteredTemplate";
                    controller = factory.CreateController(RequestContext, "DynamicPageTemplate");
                    controller.Execute(RequestContext);
                }
                else
                {
                    throw new InvalidOperationException(ex.Message, ex);
                }

            }
            factory.ReleaseController(controller);
        }
    }
}