import { useId, useState, useRef, useEffect, useMemo, type SelectHTMLAttributes } from 'react';
import { ChevronDown, Check, Search } from 'lucide-react';

export function SelectInput({
  label,
  options,
  id,
  error,
  value,
  onChange,
  disabled,
  compact,
  placeholder,
  className,
  style,
  ...props
}: SelectHTMLAttributes<HTMLSelectElement> & {
  label?: string;
  options: readonly { value: string; label: string }[];
  error?: string;
  compact?: boolean;
  placeholder?: string;
}) {
  const generated = useId();
  const inputId = id ?? generated;
  const containerRef = useRef<HTMLDivElement>(null);
  const searchInputRef = useRef<HTMLInputElement>(null);
  const [isOpen, setIsOpen] = useState(false);
  const [searchQuery, setSearchQuery] = useState('');

  const stringValue = value !== undefined && value !== null ? String(value) : '';

  // Find currently selected option
  const selectedOption = useMemo(() => {
    return options.find((opt) => String(opt.value) === stringValue);
  }, [options, stringValue]);

  // Determine placeholder vs active label
  const displayLabel = useMemo(() => {
    if (selectedOption && selectedOption.value !== '') {
      return selectedOption.label;
    }
    if (placeholder) {
      return placeholder;
    }
    // If the first option is a placeholder like "All statuses" or "Select relationship", show it
    const firstOpt = options[0];
    if (firstOpt && firstOpt.value === '') {
      return firstOpt.label;
    }
    return label ? `Select ${label.toLowerCase()}...` : 'Select...';
  }, [selectedOption, options, label, placeholder]);

  const isPlaceholder = !selectedOption || selectedOption.value === '';

  // Close when clicked outside
  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
        setIsOpen(false);
        setSearchQuery('');
      }
    }
    if (isOpen) {
      document.addEventListener('mousedown', handleClickOutside);
    }
    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
    };
  }, [isOpen]);

  // Focus search input when dropdown opens (if many options)
  useEffect(() => {
    if (isOpen && options.length > 7 && searchInputRef.current) {
      setTimeout(() => searchInputRef.current?.focus(), 50);
    }
  }, [isOpen, options.length]);

  // Filter options if search query typed
  const filteredOptions = useMemo(() => {
    if (!searchQuery.trim()) return options;
    const q = searchQuery.toLowerCase();
    return options.filter((opt) => opt.label.toLowerCase().includes(q));
  }, [options, searchQuery]);

  const handleSelect = (optValue: string) => {
    if (disabled) return;
    setIsOpen(false);
    setSearchQuery('');

    if (onChange) {
      const syntheticEvent = {
        target: {
          value: optValue,
          name: props.name || '',
          id: inputId,
        },
        currentTarget: {
          value: optValue,
          name: props.name || '',
          id: inputId,
        },
      } as unknown as React.ChangeEvent<HTMLSelectElement>;
      onChange(syntheticEvent);
    }
  };

  const dataField = (props as Record<string, unknown>)['data-field'] as string | undefined;

  return (
    <div
      className={`field custom-select-field ${compact ? 'is-compact' : ''} ${className ?? ''}`}
      ref={containerRef}
      data-field={dataField}
      style={{ position: 'relative', ...style }}
    >
      {label && (
        <label htmlFor={inputId}>
          {label}
          {props.required && (
            <span className="hr-required-mark" aria-hidden="true">
              {' '}
              *
            </span>
          )}
        </label>
      )}

      {/* Hidden native select for HTML form accessibility & test selectors */}
      <select
        {...props}
        id={inputId}
        value={stringValue}
        disabled={disabled}
        onChange={onChange}
        tabIndex={-1}
        aria-hidden="true"
        style={{
          position: 'absolute',
          opacity: 0,
          pointerEvents: 'none',
          width: 0,
          height: 0,
          margin: 0,
          padding: 0,
          border: 'none',
        }}
      >
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>

      {/* Professional Styled Trigger */}
      <div
        role="combobox"
        tabIndex={disabled ? -1 : 0}
        aria-expanded={isOpen}
        aria-haspopup="listbox"
        aria-invalid={Boolean(error)}
        aria-describedby={error ? inputId + '-error' : undefined}
        data-field={dataField}
        className={`custom-select-trigger ${compact ? 'is-compact' : ''} ${isOpen ? 'is-open' : ''} ${error ? 'is-invalid' : ''} ${disabled ? 'is-disabled' : ''}`}
        onClick={() => !disabled && setIsOpen(!isOpen)}
        onKeyDown={(e) => {
          if (disabled) return;
          if (e.key === 'Enter' || e.key === ' ' || e.key === 'ArrowDown') {
            e.preventDefault();
            setIsOpen(true);
          } else if (e.key === 'Escape') {
            setIsOpen(false);
          }
        }}
      >
        <span className={`custom-select-value ${isPlaceholder ? 'is-placeholder' : ''}`}>
          {displayLabel}
        </span>
        <ChevronDown
          size={compact ? 12 : 16}
          className={`custom-select-chevron ${isOpen ? 'is-rotated' : ''}`}
          aria-hidden="true"
        />
      </div>

      {/* Popover Dropdown Menu */}
      {isOpen && (
        <div className={`custom-select-popover ${compact ? 'is-compact' : ''}`} role="listbox">
          {options.length > 7 && (
            <div className="custom-select-search-box">
              <Search size={compact ? 11 : 14} className="custom-select-search-icon" />
              <input
                ref={searchInputRef}
                type="text"
                placeholder="Type to search..."
                value={searchQuery}
                className="custom-select-search-input"
                onChange={(e) => setSearchQuery(e.target.value)}
                onClick={(e) => e.stopPropagation()}
                onKeyDown={(e) => {
                  if (e.key === 'Escape') {
                    setIsOpen(false);
                  }
                }}
              />
            </div>
          )}

          <div className="custom-select-options-list">
            {filteredOptions.length === 0 ? (
              <div className="custom-select-empty">No matching options found</div>
            ) : (
              filteredOptions.map((opt) => {
                const isSelected = String(opt.value) === stringValue;
                const isBlank = opt.value === '';
                return (
                  <div
                    key={opt.value}
                    role="option"
                    aria-selected={isSelected}
                    className={`custom-select-option ${compact ? 'is-compact' : ''} ${isSelected ? 'is-selected' : ''} ${isBlank ? 'is-blank-opt' : ''}`}
                    onClick={() => handleSelect(opt.value)}
                  >
                    <span className="custom-select-option-label">{opt.label}</span>
                    {isSelected && !isBlank && (
                      <Check size={compact ? 12 : 14} className="custom-select-check" />
                    )}
                  </div>
                );
              })
            )}
          </div>
        </div>
      )}

      {error && (
        <p className="field-error" id={inputId + '-error'}>
          {error}
        </p>
      )}
    </div>
  );
}
