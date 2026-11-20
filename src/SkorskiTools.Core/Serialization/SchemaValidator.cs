using Json.Schema;
using System.Text.Json;

namespace SkorskiTools.Core.Serialization;

/// <summary>Evaluates a JSON document against one of the shipped JSON Schemas.</summary>
public static class SchemaValidator
{
    public static IReadOnlyList<string> Validate(JsonElement instance, string schemaPath)
    {
        if (!File.Exists(schemaPath))
            return new[] { $"Schema file not found: {schemaPath}" };

        try
        {
            // A local registry avoids collisions when several manifests share the same $id.
            var options = new BuildOptions { SchemaRegistry = new SchemaRegistry() };
            var schema = JsonSchema.FromFile(schemaPath, options);
            var results = schema.Evaluate(instance);

            if (results.IsValid)
                return Array.Empty<string>();

            var errors = new List<string>();
            Collect(results, errors);
            return errors.Count == 0
                ? new[] { "The manifest does not match its JSON Schema" }
                : errors.Distinct().ToArray();
        }
        catch (Exception ex) when (ex is IOException or JsonException or ArgumentException)
        {
            return new[] { $"Could not evaluate schema '{Path.GetFileName(schemaPath)}': {ex.Message}" };
        }
    }

    private static void Collect(EvaluationResults results, List<string> errors)
    {
        if (!results.IsValid && (results.Details?.Count ?? 0) == 0)
            errors.Add($"Schema violation at '{results.InstanceLocation}'");

        foreach (var detail in results.Details ?? Enumerable.Empty<EvaluationResults>())
            Collect(detail, errors);
    }
}
