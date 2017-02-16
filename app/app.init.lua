#!/usr/bin/env tarantool

local log = require('log')
local uuid = require('uuid')

local function init()
    box.schema.user.create('operator', {
        password = '123123', 
        if_not_exists = true
    })
    
    box.schema.user.grant('operator', 'read,write,execute', 
    'universe', nil, {
        if_not_exists = true
    })

    local users_space = box.schema.space.create('users', {
        if_not_exists = true
    })

    users_space:create_index('primary_id', {
        if_not_exists = true,
        type = 'HASH',
        unique = true,
        parts = {1, 'STRING'}
    })

    users_space:create_index('secondary_login', {
        if_not_exists = true,
        type = 'HASH',
        unique = true,
        parts = {3, 'STRING'}
    })
    
    users_space:create_index('secondary_rating', {
        if_not_exists = true,
        type = 'TREE',
        unique = false,
        parts = {5, 'INT'}
    })
end

local function load_data()
    local users_space = box.space.users

    users_space:insert{uuid.str(), 
    'Ivan Ivanov', 'ivanov', 
    'iivanov@domain.com', 10}
    
    users_space:insert{uuid.str(), 
    'Petr Petrov', 'petrov', 
    'ppetrov@domain.com', 15}
    
    users_space:insert{uuid.str(), 
    'Vasily Sidorov', 'sidorov', 
    'vsidorov@domain.com', 20}
end

function update_rating()
    for k,v in box.space.users:pairs() do
        box.space.users:update(v[1], {{'+', 5, 1}}) 
    end
end

box.cfg
{
    pid_file = nil,
    background = false,
    log_level = 5
}

box.once('init', init)
box.once('load_data', load_data)

local function users_handler(self)
    local data = { 
        users = {},
        count = box.space.users:len()
    }

    for k, v in box.space.users:pairs{} do
        table.insert(data.users, {
                id = v[1], 
                user = v[2] .. ' (' .. v[3] .. ')' , 
                rating = v[5]
        })
    end

    return self:render{ json = data }
end

local httpd = require('http.server')
local server = httpd.new(nil, 8080, {
        app_dir = '/usr/local/share/tarantool/'
    })

server:route({ path = '/', file = 'index.html.el'  })
server:route({ path = '/users'  }, users_handler)
server:start()