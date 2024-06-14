using Newtonsoft.Json;

namespace AppointmentScheduler.Utils;

public static class Deserializer<T> where T : class
{
    /// <summary>
    /// Deserializes JSON data from a stream into an object of type T.
    /// </summary>
    /// <param name="body">The stream containing JSON data.</param>
    /// <returns>The deserialized object of type T.</returns>
    public async static Task<T> Deserialize(Stream body)
    {
        if (body == null)
        {
            throw new ArgumentNullException(nameof(body), "Stream cannot be null.");
        }

        using var reader = new StreamReader(body);
        string requestBody = await reader.ReadToEndAsync();

        try
        {
            return JsonConvert.DeserializeObject<T>(requestBody) ?? throw new JsonException("Deserialized object is null.");
        }
        catch (JsonException je)
        {
            throw new JsonException("Error deserializing JSON data.", je);
        }
    }
}