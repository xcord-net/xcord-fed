// Read config from stdin
let data = '';
process.stdin.on('data', chunk => data += chunk);
process.stdin.on('end', () => {
  const config = JSON.parse(data);
  console.log(`[${config.agentId}] Bot started with token ${config.token.substring(0, 8)}...`);
  console.log(`[${config.agentId}] Parameters:`, config.parameters);
  console.log(`[${config.agentId}] Base URL: ${config.baseUrl}`);
  // Bot stays alive waiting for events
  // Full implementation would connect to SignalR hub and listen for events
});
