namespace CqrsLab.Shared
{
    public class ClienteCosmos
    {
        [Newtonsoft.Json.JsonProperty("id")]
        // *** ESTE ES EL CAMBIO CLAVE ***
        public string Id { get; set; } = string.Empty;

        [Newtonsoft.Json.JsonProperty("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [Newtonsoft.Json.JsonProperty("ruc")]
        public string? RUC { get; set; }

        [Newtonsoft.Json.JsonProperty("ciudad")]
        public string? Ciudad { get; set; }

        [Newtonsoft.Json.JsonProperty("pais")]
        public string Pais { get; set; } = string.Empty;
    }
}