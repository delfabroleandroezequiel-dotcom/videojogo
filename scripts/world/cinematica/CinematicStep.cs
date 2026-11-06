using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// One step of a Cinematic. Subclasses do one thing each (talk, move, animate...) and finish their
// Task when the cinematic may move on.
[GlobalClass]
public partial class CinematicStep : Resource
{
	public virtual Task Run(Cinematic cinematic) => Task.CompletedTask;

	// The player held the skip button: leave the world as if this step had played out, instantly
	// (an actor already at its mark, a quest already given). Called on the interrupted step and on
	// every step after it, behind a black screen. Default: nothing — a pause, a line, a camera move
	// leave no trace once the cinematic is over.
	public virtual void Skip(Cinematic cinematic) { }
}
