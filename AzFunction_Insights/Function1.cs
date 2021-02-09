using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using System.Collections.Generic;
using Microsoft.ApplicationInsights.Extensibility;

namespace AzFunction_Insights
{
    public class Function1
    {
        //Se agrego
        private readonly TelemetryClient telemetryClient;

        //Se agrego
        public Function1(TelemetryConfiguration telemetryConfiguration)
        {
            this.telemetryClient = new TelemetryClient(telemetryConfiguration);
        }

        [FunctionName("Function1")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] 
            HttpRequest req,
            ExecutionContext context, //se agrego
            ILogger log)
        {      

            log.LogInformation("C# HTTP trigger function processed a request.");
            

            DateTime start = DateTime.UtcNow;

            string name = req.Query["name"];
            int a = Convert.ToInt32(req.Query["a"]);
            int b = Convert.ToInt32(req.Query["b"]);

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name = name ?? data?.name;

            // Graba un evento personaliado
            var evt = new EventTelemetry("Funcion fue llamada.");
            evt.Context.User.Id = name;
            this.telemetryClient.TrackEvent(evt);

            int c = a + b;

            // Genere una metrica personalizada
            this.telemetryClient.GetMetric("longitud-contenido-request").TrackValue(req.ContentLength);
            this.telemetryClient.GetMetric("cantidad-usuarios-online").TrackValue(1500);
            this.telemetryClient.GetMetric("procesos-ejecucion").TrackValue(5);

            // Log de una dependencia en la tabla de dependencias
            var dependency = new DependencyTelemetry
            {
                Name = "GET api/funcion/2/",
                Target = "db-mongo-01",
                Data = "https://swapi.co/api/planets/1/",
                Timestamp = start,
                Duration = DateTime.UtcNow - start,
                Success = true
            };
            dependency.Context.User.Id = name;
            this.telemetryClient.TrackDependency(dependency);

            Guid txIdGuid = Guid.NewGuid();

            //Genera una traza
            //var traza = new TRace
            this.telemetryClient.TrackTrace("Mensaje de la traza", 
                SeverityLevel.Information, 
                new Dictionary<string,string> { { "transaccionId", txIdGuid.ToString() } });

            try
            {
                throw new Exception("Excepcion generada. Paso algo inesperado.");
            }catch(Exception e)
            {
                Guid excepctionID = Guid.NewGuid();
                var propierties = new Dictionary<string, string> { { "event-id",excepctionID.ToString()} };
                var metricas = new Dictionary<string, double> { { "medicion1", 15 } };
                this.telemetryClient.TrackException(e, propierties, metricas);
            }

            string responseMessage = string.IsNullOrEmpty(name)
                ? "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response."
                : $"Hello, {name}. La suma de a + b = {c}";

            return new OkObjectResult(responseMessage);
        }
    }
}
