namespace Bayes
{
    using Nancy;
    using Nancy.ModelBinding;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Table;

    public class IndexModule : NancyModule
    {
        public class Corpus
        {
            public List<string> Positives;
            public List<string> Negatves;
        }
        
        public IndexModule()
        {
            Post["/train/{classification}", true] = async (parameters, ct)  =>
            {
                Corpus corpus = this.Bind();
                await Train(parameters.classification, corpus);
                return HttpStatusCode.OK;
            };
        }

      
        private async Task Train(string classification, Corpus c)
        {
            var account = CloudStorageAccount.Parse("whatever");
            var tableclient = account.CreateCloudTableClient();
            var bayes =  tableclient.GetTableReference("Bayes");
            bayes.CreateIfNotExistsAsync();

            //create a giant or query.
            //increment postives if postive and total regardless
            //also get counts
            //batch update.

            var pwords = Histogram(c.Positives.SelectMany(p => WordBreak(p)));
            var nwords = Histogram(c.Positives.SelectMany(p => WordBreak(p)));
            var words = pwords.Keys.Concat(nwords.Keys).Distinct();
            //need documetn counta and word count?
            var knowntokens = await GetTokens(bayes, classification, words);

            
            foreach (var pword in pwords)
            {

            }


        }

        Dictionary<string, long> Histogram(IEnumerable<string> tokens)
        {
            var hist = new Dictionary<string, long>();
            foreach (var token in tokens)
            {
                if (hist.ContainsKey(token))
                {
                    ++hist[token];
                }
                else
                {
                    hist[token] = 1;
                }
            }
            return hist;
        }


        public async Task<TokenEntity> GetToken(CloudTable table, string classification, string token)
        {
            var filter = string.Format("({0}) AND ({1})", 
                          TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, classification),
                          TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, token));
            TableQuerySegment<TokenEntity> result = await table.ExecuteQuerySegmentedAsync(new TableQuery<TokenEntity>().Where(filter), null);
            return result.Single();
        }

        public async Task<TableQuerySegment<TokenEntity>> GetTokens(CloudTable table, string classification, IEnumerable<string> tokens)
        {

            
            var queryBuilder = new StringBuilder();
            var classfilter = TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, classification);

            queryBuilder.AppendFormat("({0}) AND (({1})", classfilter, TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, tokens.First()));

            //oh enumerable.aggreate i failed
            foreach (var token in tokens.Skip(1))
            {
                queryBuilder.AppendFormat(" OR ({0})", TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, token));
            }
            queryBuilder.Append(")");
            return await table.ExecuteQuerySegmentedAsync(new TableQuery<TokenEntity>().Where(queryBuilder.ToString()), null);
        }


        static readonly Regex r = new Regex(@"\w+");

        //do something non amuteur
        private IEnumerable<string> WordBreak(string input)
        {
            var matches = r.Matches(input.ToLower());
            foreach (Match m in matches)
            {
               yield return m.Value;
            }
        }
    }
}