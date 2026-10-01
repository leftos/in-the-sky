-- A bored child kicks the seat in front, and the parent's shushing is not working
-- (docs/design/events.md section 10, `restless-child`).

local BASE_CHANCE = 0.01
local FRONT_ASLEEP_FACTOR = 1.5
local NO_ROUND_FACTOR = 1.5
local BOREDOM_THRESHOLD = 70
local TIRED_REST = 60
local STRETCHED_WAIT_MINUTES = 3

local function clamp(value)
  return math.max(0, math.min(1, value))
end

-- The first adult of the child's group aboard.
local function find_parent(ctx, child)
  for i = 1, ctx:group_member_count(child) do
    local id = ctx:group_member(child, i)
    if ctx:age_band(id) == 'adult' then
      return id
    end
  end
  return nil
end

local function find_child(ctx)
  for i = 1, ctx.passenger_count do
    local id = ctx:passenger(i)
    if ctx:age_band(id) == 'child' and not ctx:asleep(id) and ctx:need(id, 'boredom') >= BOREDOM_THRESHOLD then
      local parent = find_parent(ctx, id)
      if parent ~= nil then
        return id, parent
      end
    end
  end
  return nil, nil
end

-- Adds a consequence on the passenger in front, who may not be there.
local function on_front(list, facts, after, need, delta)
  if facts.front ~= nil then
    list[#list + 1] = { after_minutes = after, target = facts.front, need = need, delta = delta }
  end
end

local function leave_effects(facts)
  if facts.tired then
    local list = { { after_minutes = 0, target = 'subject', line = "The kicking slows, and the child's head starts to nod." } }
    on_front(list, facts, 3, 'unease', 4)
    list[#list + 1] = { after_minutes = 3, target = facts.parent, need = 'unease', delta = 3 }
    return list
  end

  local list = { { after_minutes = 0, target = 'subject', line = 'The kicking goes on, and the seat in front jolts.' } }
  on_front(list, facts, 3, 'unease', 6)
  if facts.front_asleep then
    on_front(list, facts, 3, 'rest', 8)
  end
  list[#list + 1] = { after_minutes = 3, target = facts.parent, need = 'unease', delta = 6 }
  list[#list + 1] = { after_minutes = 8, target = 'neighbours', need = 'unease', delta = 3 }
  on_front(list, facts, 12, 'unease', 5)
  return list
end

return {
  id = 'restless-child',
  phases = { 'cruise' },

  trigger = function(ctx)
    if ctx.stage ~= 'cruise' then
      return nil
    end

    local child, parent = find_child(ctx)
    if child == nil then
      return nil
    end

    local front = ctx:front(child)
    local front_asleep = front ~= nil and ctx:asleep(front)
    local chance = BASE_CHANCE
    if front_asleep then
      chance = chance * FRONT_ASLEEP_FACTOR
    end
    if not ctx.service_round_running then
      chance = chance * NO_ROUND_FACTOR
    end
    if math.random() >= chance then
      return nil
    end

    return {
      subject = child,
      subject_seat = ctx:seat(child),
      parent = parent,
      front = front,
      front_asleep = front_asleep,
      tired = ctx:need(child, 'rest') >= TIRED_REST,
      stretched = ctx.service_round_running or ctx.longest_task_wait_minutes >= STRETCHED_WAIT_MINUTES,
    }
  end,

  describe = function(facts)
    return 'The child in ' .. facts.subject_seat
      .. ' has run out of things to do and is kicking the seat in front. The shushing from the next seat is not working.'
  end,

  choices = function(facts)
    local stretched = facts.stretched and 1 or 0
    local tired = facts.tired and 1 or 0
    local front_asleep = facts.front_asleep and 1 or 0
    return {
      {
        id = 'pack', label = 'Bring a colouring pack', needs_crew = true, minutes = 1,
        quality = clamp(0.7 + 0.2 * stretched - 0.2 * tired),
      },
      {
        id = 'play', label = 'Stop and play a game with them', needs_crew = true, minutes = 4,
        quality = clamp(0.9 - 0.5 * stretched - 0.3 * tired),
      },
      {
        id = 'leave', label = 'Leave it for now', needs_crew = false, minutes = 0,
        quality = clamp(0.2 + 0.5 * tired + 0.15 * stretched - 0.15 * front_asleep),
      },
    }
  end,

  effects = function(facts, choice)
    if choice == 'pack' then
      return {
        { after_minutes = 0, target = 'subject', line = 'A crayon pack and a paper aeroplane land on the tray table.' },
        { after_minutes = 1, target = 'subject', need = 'boredom', delta = -15 },
        { after_minutes = 5, target = 'subject', need = 'boredom', delta = -10 },
        { after_minutes = 1, target = facts.parent, need = 'unease', delta = -5 },
      }
    end

    if choice == 'play' then
      local list = {
        { after_minutes = 0, target = 'subject', line = 'The crew member crouches in the aisle for a round of I spy.' },
        { after_minutes = 2, target = 'subject', need = 'boredom', delta = -20 },
        { after_minutes = 4, target = 'subject', need = 'boredom', delta = -20 },
        { after_minutes = 4, target = facts.parent, need = 'unease', delta = -8 },
      }
      on_front(list, facts, 4, 'unease', -3)
      return list
    end

    return leave_effects(facts)
  end,
}
