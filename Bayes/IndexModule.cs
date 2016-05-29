namespace Bayes
{
    using Nancy;
    using Nancy.ModelBinding;
    using System.Collections.Generic;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Table;
    using System;

    public class IndexModule : NancyModule
    {
        public IndexModule()
        {
            Post["/train/{classification}", true] = async (parameters, ct) =>
            {
                Corpus corpus = this.Bind();
                Func<CloudTable> t = BayesTable;
                var bb = new BinaryBayes(t, parameters.classification);
                await bb.Train(corpus);
                return HttpStatusCode.OK;
            };

            Get["/classify/{classification}", true] = async (parameters, ct) =>
            {
                string classification = parameters.classification;
                string input = this.Request.Query["input"];
                Func<CloudTable> t = BayesTable;
                var bb = new BinaryBayes(t, parameters.classification);
                return await bb.Classify(input);
            };
        }

        private CloudTable BayesTable()
        {
            var account = CloudStorageAccount.Parse("DefaultEndpointsProtocol=https;AccountName=bayes;AccountKey=n9Orh +88xOI/g+Hc1HgKL3jj8515VV8Te0QfMPG3WKuM1vC/0MC8LecWWFXQqzcpxP8PQeN+yHq22PzLD9HTcg==");
            var tableclient = account.CreateCloudTableClient();
            return tableclient.GetTableReference("Bayes");
        }
    }
}
 