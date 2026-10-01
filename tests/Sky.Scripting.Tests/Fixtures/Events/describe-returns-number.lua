-- The fault the scene check exists for: describe has to return a string the report can print, and the host disables a
-- module whose scene is anything else.
return {
  id = 'describe-returns-number',
  phases = { 'cruise' },
  trigger = function(ctx)
    if ctx.stage ~= 'cruise' then
      return nil
    end

    local subject = ctx:passenger(1)
    if subject == nil then
      return nil
    end

    return { subject = subject }
  end,
  describe = function(facts)
    return 42
  end,
  choices = function(facts)
    return {
      { id = 'help', label = 'Step in and help', needs_crew = true, minutes = 2, quality = 0.8 },
      { id = 'leave', label = 'Leave it for now', needs_crew = false, minutes = 0, quality = 0.2 },
    }
  end,
  effects = function(facts, choice)
    return { { after_minutes = 0, target = 'subject', line = 'They settle back into their seat.' } }
  end,
}
