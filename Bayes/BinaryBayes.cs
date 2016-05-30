using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace Bayes
{
    public class Corpus
    {
        public List<string> Positives = new List<string>();
        public List<string> Negatives =  new List<string>();
        public string Details;
    }

    public class BinaryBayes
    {
        public BinaryBayes(Func<CloudTable> table, string classification)
        {
            _table = table;
            _class = classification;
        }
        private Func<CloudTable> _table;
        private string _class;
        private readonly static string DocumentToken = "_document";
        //private readonly static string WordToken = "_word";

        public async Task<IEnumerable<TokenEntity>> Train(Corpus c)
        {
            var pwords = Histogram(c.Positives.SelectMany(p => WordBreak(p)));
            var nwords = Histogram(c.Positives.SelectMany(p => WordBreak(p)));
            var words = pwords.Keys.Concat(nwords.Keys).Concat(new[] { DocumentToken }).Distinct();
            CloudTable table =_table();
            table.CreateIfNotExists();
            var knowntokens = await GetTokens(table, words);
            var tokendict = knowntokens.ToDictionary(t => t.Token, t => t);

            var updates = new List<Task<TokenEntity>>();
            foreach (var word in words)
            {
                var token = GetOrCreateToken(tokendict, word);
                updates.Add(Update(token, pwords[word], nwords[word]));
            }

            //don't sure we neeed this or not. Skipping unique&total word count
            var doctoken = GetOrCreateToken(tokendict, DocumentToken);
            doctoken.Postives += c.Positives.Count();
            doctoken.Negatives += c.Negatives.Count();
            updates.Add(Update(doctoken, c.Positives.Count(), c.Negatives.Count()));

            return await Task.WhenAll(updates);
        }

        public async Task<double> Classify(string input)
        {
            var tokens = await GetTokens(_table(), WordBreak(input));
            return tokens.Aggregate(1.0, (current, next) => current * next.Probabilty);
        }

        private async Task<TokenEntity> Update(TokenEntity orig, long pos, long neg)
        {

            var table = _table();
            int backoff = 1;
            do
            {
                try
                {
                    var newtoken = orig.Increment(pos, neg);
                    await table.ExecuteAsync(TableOperation.InsertOrReplace(newtoken));
                    return newtoken;
                }
                catch (StorageException ex)
                {
                    if (ex.RequestInformation.HttpStatusCode != (int)HttpStatusCode.PreconditionFailed &&
                        ex.RequestInformation.HttpStatusCode != (int)HttpStatusCode.Conflict)
                        throw;
                    await Task.Delay(backoff * 100);
                    backoff *= 2;
                    orig = await GetToken(table, orig.Token);
                }
            } while (backoff < 256);
            throw new Exception("failed to update " + orig);
        }

        private TokenEntity GetOrCreateToken(Dictionary<string, TokenEntity> dict, string tokenkey)
        {
            TokenEntity token;
            if (!dict.TryGetValue(tokenkey, out token))
            {
                return new TokenEntity(_class, tokenkey);
            }
            return token;
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


        private async Task<TokenEntity> GetToken(CloudTable table, string token)
        {
            //todo cache lochalle
            var filter = string.Format("({0}) AND ({1})",
                          TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, _class),
                          TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, token));
            TableQuerySegment<TokenEntity> result = await table.ExecuteQuerySegmentedAsync(new TableQuery<TokenEntity>().Where(filter), null);
            return result.Single();
        }

        private async Task<TableQuerySegment<TokenEntity>> GetTokens(CloudTable table, IEnumerable<string> tokens)
        {
            //shoving this all in one partition doens't make sense if we have one huge shared classifier but is great if everyone has their own (could bootstrap ones off
            //todo cache locally
            var queryBuilder = new StringBuilder();
            var classfilter = TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, _class);

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
        //normalize numbers.
        //pull out hyperlinks
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
