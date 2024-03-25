namespace YYHEggEgg.Logger
{
    public class SuggestionResult
    {
        /// <summary>
        /// The suggestion based on the context.
        /// </summary>
        public IList<string>? Suggestions { get; set; }
        /// <summary>
        /// The start index in the provided text to fill in the suggestions (replace). Only apply to the first suggestion.
        /// </summary>
        public int StartIndex { get; set; }
        /// <summary>
        /// The end index in the provided text to fill in the suggestions (replace). If want to replace all contents after user's cursor, set -1. Only apply to the first suggestion.
        /// </summary>
        public int EndIndex { get; set; } = -1;
    }

    public interface IAutoCompleteHandler
    {
        /// <summary>
        /// Get the suggestions based on the current input.
        /// </summary>
        /// <param name="text">The full line of input.</param>
        /// <param name="index">The position where the user's cursor on.</param>
        SuggestionResult GetSuggestions(string text, int index);
    }
}
