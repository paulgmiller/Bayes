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

        private CloudTable Bayes()
        {
            var account = CloudStorageAccount.Parse("whatever");
            var tableclient = account.CreateCloudTableClient();
            var bayes = tableclient.GetTableReference("Bayes");
            bayes.CreateIfNotExistsAsync().Wait();
            return bayes;
        }

        private readonly static string DocumentToken = "_document";
        private readonly static string WordToken = "_word";

        private async Task Train(string classification, Corpus c)
        {
            var counts = new[] { DocumentToken, WordToken };
            var pwords = Histogram(c.Positives.SelectMany(p => WordBreak(p)));
            var nwords = Histogram(c.Positives.SelectMany(p => WordBreak(p)));
            var words = pwords.Keys.Concat(nwords.Keys).Concat(counts). Distinct();
            
            var knowntokens = await GetTokens(Bayes(), classification, words);
            var tokendict = knowntokens.ToDictionary(t => t.Token, t => t);

            foreach (var pword in pwords)
            {
                var token = GetOrAddToken(tokendict, classification, pword.Key);
                token.Postives += pword.Value;
                token.Total += pword.Value;
            }
            foreach (var nword in nwords)
            {
                var token = GetOrAddToken(tokendict, classification, nword.Key);
                token.Total += nword.Value;
            }

            var doctoken = GetOrAddToken(tokendict, classification, DocumentToken);
            doctoken.Postives += c.Positives.Count();
            doctoken.Total += c.Positives.Count() + c.Negatves.Count();

            var wordtoken = GetOrAddToken(tokendict, classification, WordToken);
            doctoken.Postives += c.Positives.Count();
            doctoken.Total += c.Positives.Count() + c.Negatves.Count();



        }

        /*
         public double IsInClassProbability(string className, string text) 
85         { 
86             var words = text.ExtractFeatures(); 
87             var classResults = _classes 
88                 .Select(x => new 
89                 { 
90                     Result = Math.Pow(Math.E, Calc(x.NumberOfDocs, _countOfDocs, words, x.WordsCount, x, _uniqWordsCount)), 
91                     ClassName = x.Name 
92                 }); 
93 
 
94 
 
95             return classResults.Single(x=>x.ClassName == className).Result / classResults.Sum(x=>x.Result); 
96         } 
97 
 
98         private static double Calc(double dc, double d, List<String> q, double lc, ClassInfo @class, double v) 
99         { 
100             return Math.Log(dc / d) + q.Sum(x =>Math.Log((@class.NumberOfOccurencesInTrainDocs(x) + 1) / (v + lc)));  
101         } 

        */

        private static TokenEntity GetOrAddToken(Dictionary<string, TokenEntity> dict, string classification, string tokenkey)
        {
            TokenEntity token; 
            if (!dict.TryGetValue(tokenkey, out token))
            {
                token = new TokenEntity(classification, tokenkey); ;
                dict[tokenkey] = token;
            }
            return token;
        }

        private async Task<double> Classify(string classification, string input)
        {
            var tokens = await GetTokens(Bayes(), classification, WordBreak(input));
            return tokens.Sum(t => t.Probabilty);
        }

        public Dictionary<string, long> Histogram(IEnumerable<string> tokens)
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
            //todo cache lochalle
            var filter = string.Format("({0}) AND ({1})", 
                          TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, classification),
                          TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, token));
            TableQuerySegment<TokenEntity> result = await table.ExecuteQuerySegmentedAsync(new TableQuery<TokenEntity>().Where(filter), null);
            return result.Single();
        }

        public async Task<TableQuerySegment<TokenEntity>> GetTokens(CloudTable table, string classification, IEnumerable<string> tokens)
        {
            //todo cache locally
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
 