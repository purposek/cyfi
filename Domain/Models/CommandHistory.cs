using Domain.Enums;

namespace Domain.Models
{
    public static class CommandHistory
    {
        private const int _capacity = 10;
        private static Queue<InputCommand> _latestCommands = new Queue<InputCommand>(_capacity);

        public static void AddCommand(InputCommand command)
        {
            if (_latestCommands.Count == _capacity)
            {
                _latestCommands.Dequeue();
            }

            _latestCommands.Enqueue(command);
        }

        public static bool Contains(InputCommand command)
        {
            return _latestCommands.Contains(command);
        }

        public static List<InputCommand> GetLatestCommands()
        {
            return _latestCommands.ToList();
        }
    }
}
