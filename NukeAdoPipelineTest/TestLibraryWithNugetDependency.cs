using Newtonsoft.Json;

namespace PublicNukeTest
{
    public class TestLibraryWithNugetDependency
    {
        public TestLibraryWithNugetDependency(string toSerialize)
        {
            Serialized = JsonConvert.SerializeObject(toSerialize);
        }

        public string Serialized { get; }
    }
}
