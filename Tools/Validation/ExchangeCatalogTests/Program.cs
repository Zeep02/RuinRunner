using System.Text;
using System.Text.Json;
using Newtonsoft.Json;
using RuinRunner.Backend;
using RuinRunner.Api;

var root=Path.GetFullPath(args[0]);var checks=new List<string>();
void Check(bool ok,string name){if(!ok)throw new Exception(name);checks.Add(name);}
ContentCatalog Client(string json)=>JsonConvert.DeserializeObject<ContentCatalog>(json);
Catalog Server(string json)=>System.Text.Json.JsonSerializer.Deserialize<Catalog>(json,new JsonSerializerOptions(JsonSerializerDefaults.Web));
var jsons=new[]{"campaign-v1","campaign-v2"}.Select(v=>File.ReadAllText(Path.Combine(root,"Backend/Content/catalog."+v+".json"),Encoding.UTF8)).ToArray();
foreach(var json in jsons)
{
    var client=Client(json);var server=Server(json);CatalogStore.ValidateExchangeRecipes(server);
    var bound=ExchangeCatalogBinding.Prepare(client);
    Check(bound.Count==server.ExchangeRecipes.Length,client.version+" delivered recipe count");
    for(int i=0;i<bound.Count;i++)
        Check(bound[i].Id==server.ExchangeRecipes[i].Id&&bound[i].OutputId==server.ExchangeRecipes[i].OutputId
            &&bound[i].InputIds.SequenceEqual(server.ExchangeRecipes[i].InputIds),client.version+" server/client recipe match "+i);
}
var original=Client(jsons[0]);var pinned=ExchangeCatalogBinding.Prepare(original);var originalInput=pinned[0].InputIds[0];
original.exchangeRecipes[0].inputIds[0]="changed-after-binding";
Check(pinned[0].InputIds[0]==originalInput,"pinned snapshot survives transport object mutation");
var current=Client(jsons[1]);current.exchangeRecipes[0].id="independent-recipe-id";
var newer=ExchangeCatalogBinding.Prepare(current);
Check(newer[0].Id!=newer[0].OutputId&&newer[0].OutputId==Client(jsons[1]).exchangeRecipes[0].outputId,"recipe ID is independent of output ID");
Check(pinned[0].Id!=newer[0].Id,"new catalog binding cannot change existing run snapshot");
var invalid=new Action<ContentCatalog>[] {
    c=>c.exchangeRecipes[0].inputIds=null,
    c=>c.exchangeRecipes[0].inputIds=new[]{c.exchangeRecipes[0].inputIds[0]},
    c=>c.exchangeRecipes[0].inputIds[1]=c.exchangeRecipes[0].inputIds[0],
    c=>c.exchangeRecipes[1].id=c.exchangeRecipes[0].id,
    c=>c.exchangeRecipes[0].id=" ",
    c=>c.exchangeRecipes[0].inputIds[0]="missing-item",
    c=>c.exchangeRecipes[0].outputId="rust_sword",
    c=>c.exchangeRecipes[0]=null
};
for(int i=0;i<invalid.Length;i++)
{
    var c=Client(jsons[1]);invalid[i](c);var json=JsonConvert.SerializeObject(c);bool clientRejected=false,serverRejected=false;
    try{ExchangeCatalogBinding.Prepare(c);}catch(ArgumentException){clientRejected=true;}
    try{CatalogStore.ValidateExchangeRecipes(Server(json));}catch(ApiFailure){serverRejected=true;}
    Check(clientRejected&&serverRejected,"invalid recipe rejected by server and client "+i);
}
var path=Path.Combine(root,"Logs/Validation/2026-10-09-goal/exchange-catalog-"+DateTime.Now.ToString("HHmmss")+".json");
File.WriteAllText(path,JsonConvert.SerializeObject(new{status="PASS",checks,scope="Published catalog UTF-8 bytes -> production server DTO/validation and Unity DTO/immutable binding; no gameplay grants or user state"},Formatting.Indented),new UTF8Encoding(false));
Console.WriteLine("PASS "+checks.Count+" "+path);
