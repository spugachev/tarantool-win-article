using System;
using System.Threading.Tasks;
using ProGaudi.Tarantool.Client;
using ProGaudi.Tarantool.Client.Model;
using ProGaudi.Tarantool.Client.Model.Enums;
using ProGaudi.Tarantool.Client.Model.UpdateOperations;

namespace ConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            DoWork().Wait();
        }

        static async Task DoWork()
        {
            using (var box = await Box.Connect(
                "operator:123123@localhost:3301"))
            {
                var schema = box.GetSchema();

                var space = await schema.GetSpace("users");
                var primaryIndex = await space.GetIndex("primary_id");

                //await space.Insert(TarantoolTuple.Create(Guid.NewGuid().ToString(),
                //"Vladimir Vladimirov", "vvladimirov", "vvladimirov@domsin.com", 10L));

                var updatedData = await space.Update<TarantoolTuple<string>,
                TarantoolTuple<string, string, string, string, long>>(
                TarantoolTuple.Create("600ca93b-10dc-4ebe-8c78-95f4d5384426"),
                new UpdateOperation[] { UpdateOperation.CreateAssign(4, 47L) });

                var data = await primaryIndex.Select<TarantoolTuple<string>,
                    TarantoolTuple<string, string, string, string, long>>(
                    TarantoolTuple.Create(String.Empty), new SelectOptions
                    {
                        Iterator = Iterator.All
                    });

                var loginIndex = await space.GetIndex("secondary_login");
                var users = await loginIndex.Select<TarantoolTuple<string>,
                    TarantoolTuple<string, string, string, string, long>>(
                    TarantoolTuple.Create("petrov"));
                var petrov = users.Data;

                var ratingIndex = await space.GetIndex("secondary_rating");
                var ratingUsers = await ratingIndex.Select<TarantoolTuple<long>,
                    TarantoolTuple<string, string, string, string, long>>(
                    TarantoolTuple.Create(15L), new SelectOptions
                    {
                        Iterator = Iterator.Ge
                    });

                await box.Call("update_rating");

                foreach (var item in data.Data)
                {
                    Console.WriteLine(item);
                }
            }
        }
    }
}
