using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;
using ADSUS_BE.Controllers;

namespace ADSUS_BE.UnitTests.Controllers
{
    public class RoleAccessTests
    {
        [Fact]
        public void MedicinesController_MutatingEndpoints_ShouldNotAllowDoctor()
        {
            var controllerType = typeof(MedicinesController);
            var methods = controllerType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

            foreach (var method in methods)
            {
                var isGet = method.GetCustomAttributes().Any(a => a is HttpMethodAttribute && a.GetType().Name == "HttpGetAttribute");
                var authorizeAttr = method.GetCustomAttribute<AuthorizeAttribute>();
                
                if (authorizeAttr != null)
                {
                    if (!isGet)
                    {
                        Assert.False(authorizeAttr.Roles?.Contains("DOCTOR"), $"Method {method.Name} is not a GET method but allows DOCTOR.");
                    }
                }
            }
        }

        [Fact]
        public void InventoryController_MutatingEndpoints_ShouldNotAllowDoctor()
        {
            var controllerType = typeof(InventoryController);
            
            // Controller level authorize
            var classAuthorizeAttr = controllerType.GetCustomAttribute<AuthorizeAttribute>();
            bool classAllowsDoctor = classAuthorizeAttr?.Roles?.Contains("DOCTOR") ?? false;

            var methods = controllerType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

            foreach (var method in methods)
            {
                var isGet = method.GetCustomAttributes().Any(a => a is HttpMethodAttribute && a.GetType().Name == "HttpGetAttribute");
                var authorizeAttr = method.GetCustomAttribute<AuthorizeAttribute>();
                
                if (!isGet)
                {
                    if (authorizeAttr != null)
                    {
                        Assert.False(authorizeAttr.Roles?.Contains("DOCTOR"), $"Method {method.Name} is not a GET method but allows DOCTOR.");
                    }
                    else if (classAllowsDoctor)
                    {
                        Assert.Fail($"Method {method.Name} is not a GET method and inherits DOCTOR access from class.");
                    }
                }
            }
        }
    }
}
