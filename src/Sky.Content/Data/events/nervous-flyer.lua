-- A nervous flyer grips the armrests as the engines spool up (docs/design/events.md section 10, `nervous-flyer`).

local BASE_CHANCE = 0.02
local TURBULENCE_FACTOR = 2
local UNEASE_THRESHOLD = 55
local HIGH_UNEASE = 75
local BUSY_WAIT_MINUTES = 3
local TURBULENCE_LEAVE_PENALTY = 0.2

-- The reference cabin's aisle runs between C and D in both classes.
local LEFT_OF_AISLE = { A = true, B = true, C = true }

local QUALITY = {
  free_mild = { sit = 0.9, companion = 0.7, leave = 0.2 },
  free_high = { sit = 1.0, companion = 0.4, leave = 0.05 },
  busy_mild = { sit = 0.5, companion = 0.7, leave = 0.6 },
  busy_high = { sit = 0.6, companion = 0.4, leave = 0.05 },
}

local function row_of(label)
  return tonumber(string.match(label, '^(%d+)'))
end

local function side_by_side(a, b)
  return row_of(a) == row_of(b) and LEFT_OF_AISLE[string.match(a, '%a+$')] == LEFT_OF_AISLE[string.match(b, '%a+$')]
end

local function find_flyer(ctx)
  for i = 1, ctx.passenger_count do
    local id = ctx:passenger(i)
    if not ctx:asleep(id) and ctx:has_trait(id, 'nervous_flyer') and ctx:need(id, 'unease') >= UNEASE_THRESHOLD then
      return id
    end
  end
  return nil
end

-- A member of the flyer's group in the seat beside them.
local function find_companion(ctx, subject)
  local seat = ctx:seat(subject)
  for i = 1, ctx:neighbour_count(subject) do
    local id = ctx:neighbour(subject, i)
    if ctx:group(id) == ctx:group(subject) and side_by_side(seat, ctx:seat(id)) then
      return id
    end
  end
  return nil
end

return {
  id = 'nervous-flyer',
  phases = { 'taxi-out', 'climb' },

  trigger = function(ctx)
    local stage = ctx.stage
    if stage ~= 'taxi-out' and stage ~= 'climb' then
      return nil
    end

    local subject = find_flyer(ctx)
    if subject == nil then
      return nil
    end

    local chance = BASE_CHANCE
    if ctx.turbulence ~= 'none' then
      chance = chance * TURBULENCE_FACTOR
    end
    if math.random() >= chance then
      return nil
    end

    return {
      subject = subject,
      subject_seat = ctx:seat(subject),
      companion = find_companion(ctx, subject),
      high = ctx:need(subject, 'unease') >= HIGH_UNEASE,
      busy = stage == 'taxi-out' or ctx.seatbelt_sign or ctx.longest_task_wait_minutes >= BUSY_WAIT_MINUTES,
      turbulence = ctx.turbulence,
    }
  end,

  describe = function(facts)
    return 'The passenger in ' .. facts.subject_seat
      .. ' is gripping both armrests and staring at the seat back. They flinch at every new sound from the engines.'
  end,

  choices = function(facts)
    local quality = QUALITY[(facts.busy and 'busy' or 'free') .. '_' .. (facts.high and 'high' or 'mild')]
    local leave = quality.leave
    if facts.turbulence ~= 'none' then
      leave = math.max(0, leave - TURBULENCE_LEAVE_PENALTY)
    end

    local list = { { id = 'sit', label = 'Sit with them for a minute', needs_crew = true, minutes = 2, quality = quality.sit } }
    if facts.companion ~= nil then
      list[#list + 1] = {
        id = 'companion', label = 'Leave it to their companion', needs_crew = false, minutes = 0, quality = quality.companion,
      }
    end
    list[#list + 1] = { id = 'leave', label = 'Leave it for now', needs_crew = false, minutes = 0, quality = leave }
    return list
  end,

  effects = function(facts, choice)
    if choice == 'sit' then
      return {
        { after_minutes = 0, target = 'subject', line = 'The crew member crouches in the aisle and talks them through each noise.' },
        { after_minutes = 1, target = 'subject', need = 'unease', delta = -12 },
        { after_minutes = 2, target = 'subject', need = 'unease', delta = -10 },
        { after_minutes = 2, target = 'neighbours', need = 'unease', delta = -4 },
      }
    end

    if choice == 'companion' then
      return {
        { after_minutes = 0, target = 'subject', line = 'Their companion takes their hand and keeps talking.' },
        { after_minutes = 2, target = 'subject', need = 'unease', delta = -8 },
        { after_minutes = 6, target = 'subject', need = 'unease', delta = -6 },
        { after_minutes = 2, target = facts.companion, need = 'unease', delta = 5 },
      }
    end

    local list = {
      { after_minutes = 0, target = 'subject', line = 'They sit rigid, eyes shut, breathing hard.' },
      { after_minutes = 3, target = 'subject', need = 'unease', delta = 8 },
      { after_minutes = 3, target = 'neighbours', need = 'unease', delta = 4 },
      { after_minutes = 8, target = 'subject', need = 'unease', delta = 6 },
    }
    if facts.high then
      list[#list + 1] = { after_minutes = 10, target = 'subject', incident = 'panic' }
    end
    return list
  end,
}
