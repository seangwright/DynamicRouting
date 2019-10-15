using System;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using CMS.Helpers;

using RequestContext = System.Web.Routing.RequestContext;

namespace DynamicRouting.Kentico.MVC
{
    public class DynamicRouteConstraint : IRouteConstraint
    {
        public DynamicRouteConstraint()
        {
        }

        public bool Match(HttpContextBase httpContext, Route route, string parameterName, RouteValueDictionary values, RouteDirection routeDirection)
        {
            // Check if the controller is found and has the KMVCRouteOverPathPriority attribute.
            string ControllerName = (values.ContainsKey("controller") ? values["controller"].ToString() : "");
            return CacheHelper.Cache(cs => {
                // Check if the Route that it found has the override
                var factory = ControllerBuilder.Current.GetControllerFactory();
                try
                {
                    var Controller = factory.CreateController(new RequestContext(httpContext, new RouteData(route, null)), ControllerName);
                    return Attribute.GetCustomAttribute(Controller.GetType(), typeof(DynamicRouteConstraint)) != null;
                }
                catch (Exception)
                {
                    return false;
                }
            }, new CacheSettings(1440, "KMVCRouteOverPathPriority", ControllerName));
        }

    }
}
