using ProGaudi.Tarantool.Client;
using ProGaudi.Tarantool.Client.Model.Enums;
using System.Threading.Tasks;

namespace ConsoleApp.NonProduction
{
    public class User
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Login { get; set; }
        public string Email { get; set; }
        public long Rating { get; set; }
    }

    public static class BoxWrapperTest
    {
        public static async Task DoWork()
        {
            var box = (await Box.Connect("operator:123123@localhost:3301")).Wrap();

            var space = box.Space("users");
            var primaryIndex = space.Index("primary_id");
            var ratingIndex = space.Index("secondary_rating");

            var all = await primaryIndex.Select<User>();
            var user = await primaryIndex.Select<User>("1b30d7af-5f43-44a5-872b-d7bc38fa4aa9");

            var top = await ratingIndex.Select<User>(20, Iterator.Ge);
            var low = await ratingIndex.Select<User>(20, Iterator.Lt);
        }
    }
}
