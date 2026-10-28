using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// One step of a Cinematic. Subclasses do one thing each (talk, move, animate...) and finish their
// Task when the cinematic may move on.
[GlobalClass]
public partial class CinematicStep : Resource
{
	public virtual Task Run(Cinematic cinematic) => Task.CompletedTask;
}
