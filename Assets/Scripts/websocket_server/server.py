import asyncio
import websockets

# A set to keep track of all active Unity scene connections
CONNECTED_CLIENTS = set()

async def network_handler(websocket):
    # Register the incoming connection (Dashboard or Physical Sim)
    CONNECTED_CLIENTS.add(websocket)
    print(f"🔌 [Server] New client connected from: {websocket.remote_address}")
    
    try:
        # Continuously listen for incoming messages from this client
        async for message in websocket:
            print(f"📥 [Command Received]: {message}")
            
            # If it's one of our override commands, broadcast it to EVERYONE else
            if message in ["FORCE_OPEN", "FORCE_CLOSE", "RESUME_AUTO"]:
                print(f"📡 [Broadcasting]: Relaying '{message}' to all connected scenes...")
                
                # Create a list of disconnected clients to clean up later
                dead_clients = []
                
                for client in CONNECTED_CLIENTS:
                    if client != websocket: # Don't send it back to the dashboard that clicked it
                        try:
                            await client.send(message)
                        except websockets.ConnectionClosed:
                            dead_clients.append(client)
                
                # Clear out any dead connections safely
                for client in dead_clients:
                    CONNECTED_CLIENTS.remove(client)
                    
    except websockets.ConnectionClosed:
        print(f"❌ [Server] Client disconnected unexpectedly: {websocket.remote_address}")
    finally:
        # Safe cleanup when a scene stops playing
        if websocket in CONNECTED_CLIENTS:
            CONNECTED_CLIENTS.remove(websocket)
            print(f"🗑️ [Server] Removed client registration for: {websocket.remote_address}")

async def main():
    # Runs the server on localhost using port 8080 (matching your Unity script URL)
    print("🚀 [Server] WebSocket Central Station booting up...")
    print("🚀 [Server] Listening on: ws://localhost:8080")
    
    async with websockets.serve(network_handler, "localhost", 8080):
        await asyncio.Future() # Keeps the server running forever

if __name__ == "__main__":
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        print("\n🛑 [Server] WebSocket Central Station shut down cleanly.")