namespace Bayes
{
    using Nancy;
    using Nancy.ModelBinding;
    using System.Collections.Generic;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Table;
    using Microsoft.WindowsAzure.Storage.Blob;
    using System.Threading.Tasks;
    using System.Configuration;
    using Newtonsoft.Json;
    using System;

    public class IndexModule : NancyModule
    {
        public IndexModule()
        {
            Post["/train/{classification}", true] = async (parameters, ct) =>
            {
                Corpus corpus = this.Bind();
                await SaveAndTrain(parameters.classification, corpus);
                return HttpStatusCode.OK;
            };

            Get["/ptrain/{classification}", true] = async (parameters, ct) =>
            {
                var c = new Corpus();
                c.Positives.Add(this.Request.Query["input"]);
                await SaveAndTrain(parameters.classification, c);
                return HttpStatusCode.OK;
            };

            Get["/ntrain/{classification}", true] = async (parameters, ct) =>
            {
                var c = new Corpus();
                c.Negatives.Add(this.Request.Query["input"]);
                await SaveAndTrain(parameters.classification, c);
                return HttpStatusCode.OK;
            };

            Get["/classify/{classification}", true] = async (parameters, ct) =>
            {
                string input = this.Request.Query["input"];
                Func<CloudTable> t = BayesTable;
                var bb = new BinaryBayes(t, parameters.classification);
                return await bb.Classify(input);
            };
        }

        public async Task<string> SaveAndTrain(string classification, Corpus c)
        {
            Func<CloudTable> t = BayesTable;
            var bb = new BinaryBayes(t, classification);
            var corpi = Corpi(classification);
            await corpi.UploadTextAsync(JsonConvert.SerializeObject(c));
            return JsonConvert.SerializeObject(await bb.Train(c));
        }
        private CloudTable BayesTable()
        {
            var cs = ConfigurationManager.ConnectionStrings["bayes"].ConnectionString;
            var account = CloudStorageAccount.Parse(cs);
            var tableclient = account.CreateCloudTableClient();
            return tableclient.GetTableReference("Bayes");
        }

        private CloudBlockBlob Corpi(string classification)
        {
            var cs = ConfigurationManager.ConnectionStrings["bayes"].ConnectionString;
            var account = CloudStorageAccount.Parse(cs);
            var client = account.CreateCloudBlobClient();
            var container = client.GetContainerReference(classification);
            return container.GetBlockBlobReference(Guid.NewGuid().ToString());

        }
    }
}
 