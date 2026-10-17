namespace Wick.Sim
{
    public enum CommandType : byte
    {
        Dash = 0,
        RunStart = 1,
        RunStop = 2,
        TiptoeOn = 3,
        TiptoeOff = 4,
        Decoy = 5,
    }

    /// <summary>A player command, applied at step 1 of tick <see cref="Tick"/>.</summary>
    public readonly struct Command
    {
        public readonly int Tick;
        public readonly CommandType Type;

        /// <summary>Direction for Dash and RunStart; always N for other types.</summary>
        public readonly Dir Dir;

        public Command(int tick, CommandType type, Dir dir)
        {
            Tick = tick;
            Type = type;
            Dir = HasDir(type) ? dir : Dir.N;
        }

        public static Command Dash(int tick, Dir dir) => new Command(tick, CommandType.Dash, dir);
        public static Command RunStart(int tick, Dir dir) => new Command(tick, CommandType.RunStart, dir);
        public static Command RunStop(int tick) => new Command(tick, CommandType.RunStop, Dir.N);
        public static Command TiptoeOn(int tick) => new Command(tick, CommandType.TiptoeOn, Dir.N);
        public static Command TiptoeOff(int tick) => new Command(tick, CommandType.TiptoeOff, Dir.N);
        public static Command Decoy(int tick) => new Command(tick, CommandType.Decoy, Dir.N);

        public static bool HasDir(CommandType type)
        {
            return type == CommandType.Dash || type == CommandType.RunStart;
        }

        public override string ToString()
        {
            return HasDir(Type) ? Tick + ":" + Type + "(" + Dir + ")" : Tick + ":" + Type;
        }
    }
}
