using System.Text.Json;
using System.Text.Json.Serialization;

namespace Transiever.Common.Accounts;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(AccountProfile))]
internal partial class AccountsJsonContext : JsonSerializerContext;

internal sealed class AccountReferenceJsonConverter : JsonConverter<AccountReference>
{
    public override AccountReference Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String) throw new JsonException("Invalid account reference.");
        try { return AccountReference.Parse(reader.GetString()!); }
        catch (ArgumentException) { throw new JsonException("Invalid account reference."); }
    }

    public override void Write(Utf8JsonWriter writer, AccountReference value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());
}
