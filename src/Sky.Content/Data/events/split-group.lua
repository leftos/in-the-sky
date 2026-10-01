-- Two passengers booked together find their seats rows apart during boarding, and one of them asks a crew member if
-- they can sit together: the one seat-conflict event (docs/design/events.md section 10, `split-group`).

local BASE_CHANCE = 0.01
local CHILD_FACTOR = 2
local BUSY_SEATED_SHARE = 0.7

-- The reference cabin's aisle runs between C and D in both classes.
local LEFT_OF_AISLE = { A = true, B = true, C = true }

local QUALITY = {
  clear_adults = { swap = 0.7, pair = 0.9, leave = 0.3 },
  clear_child = { swap = 0.8, pair = 1.0, leave = 0.1 },
  busy_adults = { swap = 0.3, pair = 0.5, leave = 0.8 },
  busy_child = { swap = 0.5, pair = 0.7, leave = 0.35 },
}

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

-- A passenger travelling alone, in their seat, side by side with `beside`: someone who could trade seats.
local function find_swapper(ctx, beside)
  local seat = ctx:seat(beside)
  for i = 1, ctx:neighbour_count(beside) do
    local id = ctx:neighbour(beside, i)
    if ctx:group_member_count(id) == 0 and ctx:seated(id) and side_by_side(seat, ctx:seat(id)) then
      return id
    end
  end
  return nil
end

-- Two free seats side by side in the pair's class; nil when the class cannot be read from the free seats.
local function find_pair(ctx, class)
  if class == nil then
    return nil
  end
  for i = 1, ctx.free_seat_count - 1 do
    local a = ctx:free_seat(i)
    local b = ctx:free_seat(i + 1)
    if ctx:free_seat_class(i) == class and ctx:free_seat_has_free_neighbour(i) and side_by_side(a, b) then
      return { a, b }
    end
  end
  return nil
end

-- The other member of a group of two, both seated and not side by side; visited once per pair, from its lower id.
local function split_partner(ctx, id)
  if ctx:group_member_count(id) ~= 1 then
    return nil
  end
  local partner = ctx:group_member(id, 1)
  if partner < id or not ctx:seated(id) or not ctx:seated(partner) then
    return nil
  end
  if side_by_side(ctx:seat(id), ctx:seat(partner)) then
    return nil
  end
  return partner
end

-- The facts for one split pair, or nil when no move can be offered. With a child, the parent is the one who asks.
local function facts_for(ctx, first, second)
  local has_child = ctx:age_band(first) == 'child' or ctx:age_band(second) == 'child'
  local subject, partner = first, second
  if ctx:age_band(first) == 'child' then
    subject, partner = second, first
  end

  -- The one who moves trades seats with a passenger sitting beside the other.
  local mover = subject
  local swapper = find_swapper(ctx, partner)
  if swapper == nil then
    mover = partner
    swapper = find_swapper(ctx, subject)
  end

  local subject_seat = ctx:seat(subject)
  local partner_seat = ctx:seat(partner)
  local class = class_of_row(ctx, row_of(subject_seat)) or class_of_row(ctx, row_of(partner_seat))
  local pair = find_pair(ctx, class)
  if swapper == nil and pair == nil then
    return nil
  end

  return {
    subject = subject,
    partner = partner,
    subject_seat = subject_seat,
    partner_seat = partner_seat,
    has_child = has_child,
    mover = swapper and mover,
    swapper = swapper,
    swapper_seat = swapper and ctx:seat(swapper),
    pair = pair,
    pair_row = pair and string.match(pair[1], '^(%d+)'),
    aisle_busy = ctx.boarding_seated_share < BUSY_SEATED_SHARE,
  }
end

local function both(facts, after, need, delta)
  return {
    { after_minutes = after, target = 'subject', need = need, delta = delta },
    { after_minutes = after, target = facts.partner, need = need, delta = delta },
  }
end

local function append(list, items)
  for _, item in ipairs(items) do
    list[#list + 1] = item
  end
  return list
end

return {
  id = 'split-group',
  phases = { 'boarding' },

  trigger = function(ctx)
    if ctx.stage ~= 'boarding' then
      return nil
    end

    for i = 1, ctx.passenger_count do
      local id = ctx:passenger(i)
      local partner = split_partner(ctx, id)
      local facts = partner and facts_for(ctx, id, partner)
      if facts ~= nil then
        local chance = BASE_CHANCE
        if facts.has_child then
          chance = chance * CHILD_FACTOR
        end
        if math.random() >= chance then
          return nil
        end
        return facts
      end
    end
    return nil
  end,

  describe = function(facts)
    if facts.has_child then
      return 'A parent and child have seats in ' .. facts.subject_seat .. ' and ' .. facts.partner_seat
        .. ', rows apart. The parent stops a crew member in the aisle to ask if they can sit together.'
    end
    return 'Two passengers travelling together have seats in ' .. facts.subject_seat .. ' and ' .. facts.partner_seat
      .. '. One of them stops a crew member in the aisle to ask if they can sit together.'
  end,

  choices = function(facts)
    local key = (facts.aisle_busy and 'busy' or 'clear') .. '_' .. (facts.has_child and 'child' or 'adults')
    local quality = QUALITY[key]
    local list = {}
    if facts.swapper ~= nil then
      list[#list + 1] = { id = 'swap', label = 'Ask a neighbour to swap', needs_crew = true, minutes = 3, quality = quality.swap }
    end
    if facts.pair ~= nil then
      list[#list + 1] = { id = 'pair', label = 'Move them to an empty pair', needs_crew = true, minutes = 3, quality = quality.pair }
    end
    list[#list + 1] = { id = 'leave', label = 'Leave it for now', needs_crew = false, minutes = 0, quality = quality.leave }
    return list
  end,

  effects = function(facts, choice)
    if choice == 'swap' then
      local line = 'The crew member leans in to ' .. facts.swapper_seat .. ' and asks if they would mind moving.'
      local list = {
        { after_minutes = 0, target = 'subject', line = line },
        { after_minutes = 2, swap = { facts.mover, facts.swapper } },
      }
      append(list, both(facts, 2, 'unease', -10))
      list[#list + 1] = { after_minutes = 2, target = facts.swapper, need = 'unease', delta = 8 }
      return append(list, both(facts, 5, 'unease', -6))
    end

    if choice == 'pair' then
      local line = 'The crew member points out two empty seats in row ' .. facts.pair_row .. '.'
      local list = {
        { after_minutes = 0, target = 'subject', line = line },
        { after_minutes = 2, target = 'subject', to_seat = facts.pair[1] },
        { after_minutes = 2, target = facts.partner, to_seat = facts.pair[2] },
      }
      append(list, both(facts, 2, 'unease', -10))
      return append(list, both(facts, 5, 'unease', -6))
    end

    -- A child left apart from its parent keeps the lasting Unease modifier passengers.md gives a split family.
    local list = { { after_minutes = 0, target = 'subject', line = 'They wave to each other over the seat backs.' } }
    append(list, both(facts, 5, 'unease', 6))
    return append(list, both(facts, 15, 'unease', 5))
  end,
}
