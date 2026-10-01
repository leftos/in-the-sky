-- Two strangers side by side start arguing over the armrest (docs/design/events.md section 10, `armrest-dispute`).

local BASE_CHANCE = 0.004
local SHORT_TEMPERED_FACTOR = 3
local TIRED_FACTOR = 1.5
local FULL_CABIN_FACTOR = 1.5
local FULL_CABIN_SHARE = 0.9
local UNEASE_THRESHOLD = 55
local FIERCE_UNEASE = 75
local THIRSTY_REFRESHMENT = 50
local SLEEPY_REST = 70
local BUSY_WAIT_MINUTES = 3

-- The reference cabin's aisle runs between C and D in both classes.
local LEFT_OF_AISLE = { A = true, B = true, C = true }

local function row_of(label)
  return tonumber(string.match(label, '^(%d+)'))
end

local function side_by_side(a, b)
  return row_of(a) == row_of(b) and LEFT_OF_AISLE[string.match(a, '%a+$')] == LEFT_OF_AISLE[string.match(b, '%a+$')]
end

-- The cabin class of a row, read from the free seats: business rows come before economy rows, so a free business seat
-- at row r makes every row up to r business, and a free economy seat at row r makes every row from r on economy.
local function class_of_row(ctx, row)
  for i = 1, ctx.free_seat_count do
    local free_row = row_of(ctx:free_seat(i))
    local class = ctx:free_seat_class(i)
    if class == 'business' and row <= free_row then
      return 'business'
    end
    if class == 'economy' and row >= free_row then
      return 'economy'
    end
  end
  return nil
end

local function heated(ctx, id)
  return not ctx:asleep(id) and ctx:need(id, 'unease') >= UNEASE_THRESHOLD
end

-- The first two heated strangers side by side, in passenger order.
local function find_row(ctx)
  for i = 1, ctx.passenger_count do
    local a = ctx:passenger(i)
    if heated(ctx, a) then
      local seat = ctx:seat(a)
      for j = 1, ctx:neighbour_count(a) do
        local b = ctx:neighbour(a, j)
        if heated(ctx, b) and ctx:group(b) ~= ctx:group(a) and side_by_side(seat, ctx:seat(b)) then
          return a, b
        end
      end
    end
  end
  return nil, nil
end

-- A free seat in the row's class and not beside it; nil when the class cannot be read from the free seats.
local function find_free_seat(ctx, seat)
  local class = class_of_row(ctx, row_of(seat))
  if class == nil then
    return nil
  end
  for i = 1, ctx.free_seat_count do
    local label = ctx:free_seat(i)
    if ctx:free_seat_class(i) == class and not side_by_side(label, seat) then
      return label
    end
  end
  return nil
end

local function chance_for(ctx, a, b)
  local chance = BASE_CHANCE
  if ctx:has_trait(a, 'short_tempered') or ctx:has_trait(b, 'short_tempered') then
    chance = chance * SHORT_TEMPERED_FACTOR
  end
  if ctx:need(a, 'rest') >= SLEEPY_REST or ctx:need(b, 'rest') >= SLEEPY_REST then
    chance = chance * TIRED_FACTOR
  end
  if ctx.passenger_count >= FULL_CABIN_SHARE * (ctx.passenger_count + ctx.free_seat_count) then
    chance = chance * FULL_CABIN_FACTOR
  end
  return chance
end

local function clamp(value)
  return math.max(0, math.min(1, value))
end

local function both(facts, after, need, delta)
  return {
    { after_minutes = after, target = 'subject', need = need, delta = delta },
    { after_minutes = after, target = facts.other, need = need, delta = delta },
  }
end

local function append(list, items)
  for _, item in ipairs(items) do
    list[#list + 1] = item
  end
  return list
end

local function opening(target, line)
  return { { after_minutes = 0, target = target, line = line } }
end

local function calm_effects(facts)
  local list = opening('subject', 'The crew member stands at the row and speaks quietly to both of them.')
  append(list, both(facts, 1, 'unease', -10))
  append(list, both(facts, 3, 'unease', -10))
  list[#list + 1] = { after_minutes = 3, target = 'neighbours', need = 'unease', delta = -5 }
  return list
end

-- The two Bladder steps are the drink pulse of +15 over 30 minutes (CONCEPT section 4).
local function drink_effects(facts)
  local list = opening('subject', 'Two cups arrive, and the argument stops while they drink.')
  append(list, both(facts, 2, 'refreshment', -30))
  append(list, both(facts, 2, 'unease', -8))
  append(list, both(facts, 5, 'unease', -6))
  append(list, both(facts, 5, 'bladder', 8))
  return append(list, both(facts, 20, 'bladder', 7))
end

local function reseat_effects(facts)
  local line = 'The crew member offers the passenger in ' .. facts.other_seat .. ' the empty seat at ' .. facts.free_seat
    .. ', and they take it.'
  local list = opening(facts.other, line)
  list[#list + 1] = { after_minutes = 2, target = facts.other, to_seat = facts.free_seat }
  append(list, both(facts, 2, 'unease', -15))
  append(list, both(facts, 5, 'unease', -10))
  list[#list + 1] = { after_minutes = 2, target = 'neighbours', need = 'unease', delta = -5 }
  return list
end

local function leave_effects(facts)
  local list = opening('subject', 'The voices rise over the armrest, and heads turn in the rows around.')
  append(list, both(facts, 3, 'unease', 8))
  list[#list + 1] = { after_minutes = 3, target = 'neighbours', need = 'unease', delta = 5 }
  list[#list + 1] = { after_minutes = 8, target = 'neighbours', need = 'unease', delta = 3 }
  if facts.fierce then
    list[#list + 1] = { after_minutes = 6, target = 'subject', incident = 'fight' }
    return list
  end
  return append(list, both(facts, 10, 'unease', 4))
end

return {
  id = 'armrest-dispute',
  phases = { 'cruise' },

  trigger = function(ctx)
    if ctx.stage ~= 'cruise' then
      return nil
    end

    local a, b = find_row(ctx)
    if a == nil then
      return nil
    end
    if math.random() >= chance_for(ctx, a, b) then
      return nil
    end

    local subject, other = a, b
    if ctx:need(b, 'unease') > ctx:need(a, 'unease') then
      subject, other = b, a
    end

    return {
      subject = subject,
      other = other,
      subject_seat = ctx:seat(subject),
      other_seat = ctx:seat(other),
      fierce = ctx:need(a, 'unease') >= FIERCE_UNEASE or ctx:need(b, 'unease') >= FIERCE_UNEASE,
      thirsty = ctx:need(a, 'refreshment') >= THIRSTY_REFRESHMENT and ctx:need(b, 'refreshment') >= THIRSTY_REFRESHMENT,
      sleepy = ctx:need(a, 'rest') >= SLEEPY_REST or ctx:need(b, 'rest') >= SLEEPY_REST,
      busy = ctx.service_round_running or ctx.longest_task_wait_minutes >= BUSY_WAIT_MINUTES,
      free_seat = find_free_seat(ctx, ctx:seat(a)),
    }
  end,

  describe = function(facts)
    return facts.subject_seat .. ' and ' .. facts.other_seat
      .. ' are arguing over the armrest, and the voices are getting louder. The rows around have gone quiet to listen.'
  end,

  choices = function(facts)
    local busy = facts.busy and 1 or 0
    local fierce = facts.fierce and 1 or 0
    local thirsty = facts.thirsty and 1 or 0
    local sleepy = facts.sleepy and 1 or 0
    local leave = 0.05
    if not facts.fierce then
      leave = clamp(0.25 + 0.45 * sleepy + 0.25 * busy)
    end

    local list = {
      {
        id = 'calm', label = 'Step in and calm them down', needs_crew = true, minutes = 3,
        quality = clamp(0.8 - 0.3 * busy + 0.1 * fierce),
      },
      {
        id = 'drink', label = 'Offer them both a drink', needs_crew = true, minutes = 2,
        quality = clamp(0.35 + 0.5 * thirsty - 0.1 * busy - 0.3 * fierce),
      },
    }
    if facts.free_seat ~= nil then
      list[#list + 1] = {
        id = 'reseat', label = 'Move one to a free seat', needs_crew = true, minutes = 4,
        quality = clamp(0.5 + 0.5 * fierce - 0.2 * busy),
      }
    end
    list[#list + 1] = { id = 'leave', label = 'Leave it for now', needs_crew = false, minutes = 0, quality = leave }
    return list
  end,

  effects = function(facts, choice)
    if choice == 'calm' then
      return calm_effects(facts)
    end
    if choice == 'drink' then
      return drink_effects(facts)
    end
    if choice == 'reseat' then
      return reseat_effects(facts)
    end
    return leave_effects(facts)
  end,
}
