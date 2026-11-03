using Back_End.Models;
using Microsoft.AspNetCore.Identity;
using Xunit;
using Xunit.Abstractions;

namespace Back_End.Tests
{
    public class GenerateHashTest
    {
        private readonly ITestOutputHelper _output;

        public GenerateHashTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void PrintHash()
        {
            var hasher = new PasswordHasher<User>();
            var u = new User();
            string hash = hasher.HashPassword(u, "Password123@");
            _output.WriteLine($"GENERATED_HASH: {hash}");
            Assert.NotEmpty(hash);
        }
    }
}
