lastkeys = nil
server = nil
ST_sockets = {}
nextID = 1
currentSongID = 0
newSongID = 0
titlescreenfix = 0
ignorenextchange = 0

local KEY_NAMES = { "A", "B", "s", "S", "<", ">", "^", "v", "R", "L" }

function ST_stop(id)
	local sock = ST_sockets[id]
	ST_sockets[id] = nil
	sock:close()
end

function ST_format(id, msg, isError)
	local prefix = "Socket " .. id
	if isError then
		prefix = prefix .. " Error: "
	else
		prefix = prefix .. " Received: "
	end
	return prefix .. msg
end

function ST_error(id, err)
	console:error(ST_format(id, err, true))
	ST_stop(id)
end

--Called when the server recieves a packet from a client
function ST_received(id)
	--console:log("ST_received 1")
	local sock = ST_sockets[id]
	if not sock then return end
	while true do
		local msg, err = sock:receive(1024)
		--Sending any subsequent messages to the server
		--will cause it to mute the current song
		if msg then
			ignorenextchange = 1
			muteCurrentSong()
			console:log("muting current song")
		else
			sock:send("<ACK>")
			--console:log("ST_received 4")
			if err ~= socket.ERRORS.AGAIN then
				--console:log("ST_received 5")
				console:error(ST_format(id, err, true))
				ST_stop(id)
			end
			return
		end
	end
end

--[[
function ST_scankeys()
	local keys = emu:getKeys()
	if keys ~= lastkeys then
		lastkeys = keys
		local msg = "["
		for i, k in ipairs(KEY_NAMES) do
			if (keys & (1 << (i - 1))) == 0 then
				msg = msg .. " "
			else
				msg = msg .. k;
			end
		end
		msg = msg .. "]\n"
		for id, sock in pairs(ST_sockets) do
			if sock then sock:send(msg) end
		end
	end
end
--]]

function sendNewSongID(song)
	local msg = tostring(song)
	for id, sock in pairs(ST_sockets) do
		if sock then
			sock:send(msg)
		end
	end
end

function muteCurrentSong()
	titlescreenfix = 0
	--WARN: This will override game settings!!
	--emu:write8(0x0202BD31, 0x01)
	--emu:write8(0x02024E62, 0x0)
	--song fade in/fade out params
	emu:write8(0x3006674, 0x3)
	--length of fade in/fade out?
	emu:write8(0x3006654, 0x80)
	--
	--seems to be secondary bgm, used for battle music and such
	--once paused, never comes back? best to leave it alone for now
	--emu:write8(0x3006474, 0x1)
	--length of fade in/fade out?
	--emu:write8(0x3006454, 0x80)
end

function muteTitleMusic()
	titlescreenfix = 1
	emu:write8(0x0202BD31, 0x01)
	emu:write8(0x02024E62, 0x0)
	--ID of the currently playing song
	--seems to acknowledge the special IDS, like 7FFF (stop music)
	emu:write16(0x02024E60, 0x0)
	emu:write8(0x3006674, 0x3)
	--length of fade in/fade out?
	emu:write8(0x3006654, 0x80)
end

--Reads memory offset 0x02024E60 to get currently playing song ID
function getSongID()
	newSongID = emu:read16(0x02024E60)
	--newsongstr = tostring(newSongID)
	--console:log(newsongstr)
	--currsongstr = tostring(currentSongID)
	--console:log(currsongstr)
	--check if this is a new song ID
	--[[if newSongID == 0x1 then
		muteTitleMusic()
	end
	if newSongID == 0x43 then
		muteTitleMusic()
		console:log("NOW PLAYING TITLE MUSIC | Song ID: " .. tostring(newSongID))
		currentSongID = newSongID
		--console:log(tostring(currentSongID))
		--console:log(tostring(newSongID))
		sendNewSongID(currentSongID)
	end
	if newSongId == 0x0 then
		titlescreenfix = 0
	end--]]
	if ignorenextchange == 1 then
		ignorenextchange = 0
		emu:write8(0x0202BD31, 0x00)
		return
	end
	if newSongID == currentSongID then
		return
	end
	if newSongID == 66 then
		--WARN: This will override game settings!!
		emu:write8(0x0202BD31, 0x01)
		emu:write8(0x02024E62, 0x0)
		--song fade in/fade out params
		emu:write8(0x3006674, 0x3)
		--length of fade in/fade out?
		emu:write8(0x3006654, 0x80)
	end
	if titlescreenfix == 0 then
		console:log("NEW SONG ID FOUND | Song ID: " .. tostring(newSongID))
		currentSongID = newSongID
		--console:log(tostring(currentSongID))
		--console:log(tostring(newSongID))
		sendNewSongID(currentSongID)
	end
end

--Called when accepting a socket connection to a client
function ST_accept()
	local sock, err = server:accept()
	if err then
		console:error(ST_format("Accept", err, true))
		return
	end
	local id = nextID
	nextID = id + 1
	ST_sockets[id] = sock
	sock:add("received", function() ST_received(id) end)
	sock:add("error", function() ST_error(id) end)
	console:log(ST_format(id, "Connected"))
end

local port = 8888
server = nil
while not server do
	server, err = socket.bind(nil, port)
	if err then
		if err == socket.ERRORS.ADDRESS_IN_USE then
			port = port + 1
		else
			console:error(ST_format("Bind", err, true))
			break
		end
	else
		local ok
		ok, err = server:listen()
		if err then
			server:close()
			console:error(ST_format("Listen", err, true))
		else
			console:log("Socket Server Test: Listening on port " .. port)
			server:add("received", ST_accept)
			callbacks:add("frame", getSongID)
			currentSongID = emu:read16(0x02024E60)
			--this is bad and breaks soundroom
			--titlescreenfix = 1
			--muteTitleMusic()
			--emu:write8(0x0202BD31, 0x01)
		end
	end
end