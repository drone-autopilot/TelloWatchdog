using Newtonsoft.Json;

namespace TelloWatchdog.Models
{
    public class Accelerations
    {
        [JsonProperty("x")]
        public long X { get; set; } = 0;

        [JsonProperty("y")]
        public long Y { get; set; } = 0;

        [JsonProperty("z")]
        public long Z { get; set; } = 0;
    }

    public class TelloState
    {
        [JsonProperty("pitch")]
        public long Pitch { get; set; } = 0;

        [JsonProperty("roll")]
        public long Roll { get; set; } = 0;

        [JsonProperty("yaw")]
        public long Yaw { get; set; } = 0;

        [JsonProperty("speeds")]
        public Accelerations Speeds { get; set; } = new Accelerations();

        [JsonProperty("temp_low")]
        public long TempLow { get; set; } = 0;

        [JsonProperty("temp_high")]
        public long TempHigh { get; set; } = 0;

        [JsonProperty("time_of_flight")]
        public long TimeOfFlight { get; set; } = 0;

        [JsonProperty("height")]
        public long Height { get; set; } = 0;

        [JsonProperty("battery")]
        public long Battery { get; set; } = 0;

        [JsonProperty("barometer")]
        public double Barometer { get; set; } = 0;

        [JsonProperty("time")]
        public long Time { get; set; } = 0;

        [JsonProperty("accelerations")]
        public Accelerations Accelerations { get; set; } = new Accelerations();
    }
}
