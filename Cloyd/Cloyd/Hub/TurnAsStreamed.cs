using System.Text.Json;
using ClaudeTooling.Hub.LocalApi.Contract;
using ClaudeTooling.Hub.LocalApi.Contract.TurnsOfRunningSessions;

namespace Cloyd.Hub;

/// <summary>
/// A running Claude Code's turn as one reading of the hub's stream of it, <c>GET /running-sessions/{processId}/turn/stream</c>,
/// has sent it so far: the <see cref="BlockBeingWritten"/>, each piece added to its block or beginning the next, each
/// block's end marking it ended, and the turn's end taking away a block that hadn't. Read by the one thread that reads the
/// stream.
/// </summary>
/// <remarks>
/// An ended block is kept until the next begins: Claude Code has written it to the transcript, and the conversation's
/// stream sends it a moment later, so whoever shows it keeps showing it until then.
/// </remarks>
sealed class TurnAsStreamed
{
   bool _caughtUp;

   /// <summary>The block written last: null while none is.</summary>
   public BlockBeingWritten? Block { get; private set; }

   /// <summary>Reads <paramref name="sent"/>, and returns whether <see cref="Block"/> is to be told of: once the stream has caught up, and it may have changed.</summary>
   public bool Read(ServerSentEvent sent)
   {
      switch(sent.Name)
      {
         case TurnStreamEventNames.Piece:
            Add(JsonSerializer.Deserialize<PieceInTheStream>(sent.Data, LocalApiProtocol.Json)!);
            break;
         case TurnStreamEventNames.BlockEnded:
            var ended = JsonSerializer.Deserialize<BlockEndedInTheStream>(sent.Data, LocalApiProtocol.Json)!;
            if(Block?.Id == IdOf(ended.TurnId, ended.Response, ended.Block)) Block = Block with { HasEnded = true };
            break;
         case TurnStreamEventNames.TurnEnded:
            if(Block is { HasEnded: false }) Block = null;
            break;
         case TurnStreamEventNames.CaughtUp:
            _caughtUp = true;
            break;
         default:
            return false;
      }

      return _caughtUp;
   }

   /// <summary>Adds <paramref name="piece"/> to the block it goes on, or begins its block with it.</summary>
   void Add(PieceInTheStream piece)
   {
      var id = IdOf(piece.TurnId, piece.Response, piece.Block);
      Block = Block is { HasEnded: false } goesOn && goesOn.Id == id
                 ? goesOn with { Text = goesOn.Text + piece.Text }
                 : new BlockBeingWritten(id, piece.Kind, piece.Text, HasEnded: false);
   }

   static string IdOf(string turn, int response, int block) => $"being-written:{turn}:{response}:{block}";
}
