using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace RampaSegura.Api.Common
{
    /// <summary>
    /// Marca un controlador (o acción) como disponible SOLO en el despliegue Local.
    /// En el despliegue Cloud responde 404, como si el endpoint no existiera.
    ///
    /// Es un IResourceFilter a propósito: corta ANTES de que se construya el
    /// controlador. Así, en la nube (donde los repositorios de sync ni siquiera
    /// están registrados en el contenedor) no se intenta instanciarlos —el 404
    /// sale limpio en vez de un 500 por dependencia faltante.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class LocalOnlyAttribute : Attribute, IResourceFilter
    {
        public void OnResourceExecuting(ResourceExecutingContext context)
        {
            var deployment = context.HttpContext.RequestServices.GetRequiredService<DeploymentInfo>();
            if (!deployment.IsLocal)
            {
                context.Result = new NotFoundResult();
            }
        }

        public void OnResourceExecuted(ResourceExecutedContext context) { }
    }
}
