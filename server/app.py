from fastapi import FastAPI
from pydantic import BaseModel
from typing import List, Dict

# 1) Create the app
app = FastAPI()

# 2) Define the shape of incoming data (Pydantic models)
class Obs(BaseModel):
    px: int; py: int      # player x,y
    ex: int; ey: int      # enemy (NPC) x,y
    step: int             # frame counter
    player_score: int     # rough skill proxy

class ActReq(BaseModel):
    obs: Obs

class ChatReq(BaseModel):
    history: List[str]
    lore: str
    world: Dict

# 3) Heuristic policy + adaptive difficulty
# CHANGE THIS function in server/app.py
def heuristic_action(o: Obs) -> int:
    """
    0:up, 1:down, 2:left, 3:right
    Move toward the player in Unity coords ( +Y is up ).
    """
    dx, dy = o.px - o.ex, o.py - o.ey
    if abs(dx) > abs(dy):
        return 3 if dx > 0 else 2          # right / left
    else:
        return 0 if dy > 0 else 1          # up / down  (flipped from before)

def difficulty(player_score: int) -> float:
    """
    Map score to [0.2..0.8]. 100 -> 0.5, 150 -> ~0.8, 50 -> ~0.2
    Stronger slope so it's easy to see in the demo.
    """
    val = 0.5 + 0.006 * (player_score - 100)
    return max(0.2, min(0.8, val))

# 4) Endpoints (URLs)

@app.get("/health")
def health():
    """Quick check that the server is running."""
    return {"ok": True}

@app.post("/act")
def act(req: ActReq):
    """
    Unity will POST the world state here.
    We return an NPC action and an aggression scalar for adaptive difficulty.
    """
    o = req.obs                          # Pydantic validated Obs object
    a = heuristic_action(o)              # pick action
    agg = difficulty(o.player_score)     # compute difficulty
    return {"action": int(a), "aggression": agg}

@app.get("/seed")
def seed(bits: int = 64):
    """
    Return a random seed for procedural content.
    Try Qiskit (quantum-inspired); fall back to Python PRNG if missing.
    """
    try:
        from qiskit import QuantumCircuit       # optional import
        from qiskit_aer import Aer
        qc = QuantumCircuit(6, 6)
        qc.h(range(6))                          # put qubits into superposition
        qc.measure(range(6), range(6))          # measure to get random bits
        backend = Aer.get_backend('qasm_simulator')
        counts = backend.run(qc, shots=1).result().get_counts()
        bitstr = next(iter(counts.keys()))      # e.g., "101011"
        val = int(bitstr, 2)
    except Exception:
        import random
        val = random.getrandbits(bits)
    return {"seed": val}

@app.post("/say")
def say(req: ChatReq):
    """
    Dialogue stub — returns a short line.
    Step 5 will use your OpenAI API key for real LLM responses.
    """
    return {"text": "Stay sharp. Adjusting tactics.", "latency_ms": 30}

@app.get("/metrics")
def metrics():
    """Placeholder numbers for a tiny dashboard later."""
    return {"reward_mean": 0.73, "ttk": 1.42, "winrate": 0.58}
