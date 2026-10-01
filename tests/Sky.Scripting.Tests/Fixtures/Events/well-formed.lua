-- A module of the shape the shipped event modules follow, small enough to read at a glance: it checks its own phase,
-- draws nothing, and targets only passengers its own facts recorded.
return {
  id = 'well-formed',
  phases = { 'cruise' },
  trigger = function(ctx)
    if ctx.stage ~= 'cruise' then
      return nil
    end

    local subject = ctx:passenger(1)
    if subject == nil then
      return nil
    end

    return { subject = subject, other = ctx:passenger(2) }
  end,
  describe = function(facts)
    return 'Two passengers are comparing notes about the flight ahead.'
  end,
  choices = function(facts)
    return {
      { id = 'help', label = 'Step in and help', needs_crew = true, minutes = 2, quality = 0.8 },
      { id = 'leave', label = 'Leave it for now', needs_crew = false, minutes = 0, quality = 0.2 },
    }
  end,
  effects = function(facts, choice)
    if choice ~= 'help' then
      return { { after_minutes = 0, target = 'subject', line = 'They settle back into their seats.' } }
    end

    return {
      { after_minutes = 0, target = 'subject', line = 'The crew member stops at the row.' },
      { after_minutes = 1, target = facts.other, need = 'unease', delta = -5 },
    }
  end,
}
